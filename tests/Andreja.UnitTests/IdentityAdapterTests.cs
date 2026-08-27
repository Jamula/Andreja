using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.PostgreSql;
using Andreja.Modules.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andreja.UnitTests;

public sealed class IdentityAdapterTests
{
    private static readonly ServiceProvider IdentityOptionsProvider =
        new ServiceCollection()
            .AddOptions()
            .Configure<IdentityOptions>(
                options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .BuildServiceProvider();

    [Fact]
    public void LocalIdentityOptionsRejectUnknownAuthenticationScheme()
    {
        var options = ValidOptions() with
        {
            AuthenticationScheme = "test-header",
        };

        var result = new LocalIdentityOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void LocalIdentityOptionsRejectInsecureOrMismatchedOrigins()
    {
        var options = ValidOptions() with
        {
            AllowedOrigins = ["http://andreja.local", "https://attacker.example"],
        };

        var result = new LocalIdentityOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Equal(2, result.Failures!.Count());
    }

    [Fact]
    public async Task LocalIdentityRegistersRealPasskeyStoreAndNoTestScheme()
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{LocalIdentityOptions.SectionName}:AuthenticationScheme"] =
                IdentityConstants.ApplicationScheme,
            [$"{LocalIdentityOptions.SectionName}:RelyingPartyId"] = "andreja.local",
            [$"{LocalIdentityOptions.SectionName}:AllowedOrigins:0"] = "https://andreja.local",
            [$"{LocalIdentityOptions.SectionName}:BootstrapTokenFile"] =
                Path.GetFullPath("bootstrap-token"),
            [$"{LocalIdentityOptions.SectionName}:BootstrapCeremonyLifetime"] =
                "00:07:00",
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAndrejaIdentityPostgreSql(
            "Host=localhost;Database=never_opened;Username=none");
        services.AddAndrejaLocalIdentity(
            configuration.GetRequiredSection(LocalIdentityOptions.SectionName));

        await using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IOptions<LocalIdentityOptions>>().Value);
        var manager = provider.GetRequiredService<UserManager<AspNetIdentityUser>>();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();
        var passkeyStateCookie = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.TwoFactorUserIdScheme);

        Assert.True(manager.SupportsUserPasskey);
        Assert.Equal(TimeSpan.FromMinutes(7), passkeyStateCookie.ExpireTimeSpan);
        Assert.Equal(
            CookieSecurePolicy.Always,
            passkeyStateCookie.Cookie.SecurePolicy);
        Assert.All(
            schemes,
            scheme => Assert.StartsWith(
                "Identity.",
                scheme.Name,
                StringComparison.Ordinal));
    }

    [Fact]
    public void PersistenceModelHasRequiredTenantAndIdentityConstraints()
    {
        using var database = CreateDatabase(CreateContext());
        var model = database.Model;

        var externalIdentity = model.FindEntityType(typeof(ExternalIdentity));
        Assert.NotNull(externalIdentity);
        Assert.Contains(
            externalIdentity.GetIndexes(),
            index =>
                index.IsUnique
                && index.Properties.Select(property => property.Name)
                    .SequenceEqual(["Issuer", "Subject"]));

        AssertCompositeForeignKey(model.FindEntityType(typeof(Contact)), "TenantId", "LinkedPrincipalId");
        AssertCompositeForeignKey(model.FindEntityType(typeof(Membership)), "TenantId", "PrincipalId");
        AssertCompositeForeignKey(
            model.FindEntityType(typeof(AppUser)),
            "Id",
            "PrimaryExternalIdentityId");
        Assert.NotEmpty(model.FindEntityType(typeof(Tenant))!.GetDeclaredQueryFilters());
        Assert.NotEmpty(model.FindEntityType(typeof(Principal))!.GetDeclaredQueryFilters());
        Assert.NotEmpty(model.FindEntityType(typeof(Membership))!.GetDeclaredQueryFilters());
        Assert.NotEmpty(model.FindEntityType(typeof(Contact))!.GetDeclaredQueryFilters());
    }

    [Fact]
    public void TenantWriteFailsClosedWithoutResolvedContext()
    {
        using var database = CreateDatabase(null);
        database.Tenants.Add(
            new Tenant(TenantId.New(), "TENANT", "Tenant", "local"));

        Assert.Throws<IdentityAccessDeniedException>(() => database.SaveChanges());
    }

    [Fact]
    public void TenantWriteRejectsMismatchedTenant()
    {
        var context = CreateContext();
        using var database = CreateDatabase(context);
        database.Contacts.Add(
            new Contact(ContactId.New(), TenantId.New(), "CONTACT", "Contact"));

        Assert.Throws<IdentityAccessDeniedException>(() => database.SaveChanges());
    }

    private static void AssertCompositeForeignKey(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entity,
        params string[] propertyNames)
    {
        Assert.NotNull(entity);
        Assert.Contains(
            entity.GetForeignKeys(),
            key => key.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
    }

    private static AndrejaIdentityDbContext CreateDatabase(TenantPrincipalContext? context)
    {
        var accessor = new ScopedTenantPrincipalContext();
        if (context is not null)
        {
            accessor.Set(context);
        }

        var options = new DbContextOptionsBuilder<AndrejaIdentityDbContext>()
            .UseApplicationServiceProvider(IdentityOptionsProvider)
            .UseNpgsql("Host=localhost;Database=never_opened;Username=none")
            .Options;
        return new AndrejaIdentityDbContext(options, accessor);
    }

    private static TenantPrincipalContext CreateContext() =>
        new(TenantId.New(), AppUserId.New(), PrincipalId.New(), "unit-test");

    private static LocalIdentityOptions ValidOptions() =>
        new()
        {
            AuthenticationScheme = IdentityConstants.ApplicationScheme,
            RelyingPartyId = "andreja.local",
            AllowedOrigins = ["https://andreja.local"],
            BootstrapTokenFile = Path.GetFullPath("bootstrap-token"),
        };
}
