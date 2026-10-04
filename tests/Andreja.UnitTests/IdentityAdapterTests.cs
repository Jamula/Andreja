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

[TestClass]
public sealed class IdentityAdapterTests
{
    private static readonly ServiceProvider IdentityOptionsProvider =
        new ServiceCollection()
            .AddOptions()
            .Configure<IdentityOptions>(
                options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .BuildServiceProvider();

    [TestMethod]
    public void LocalIdentityOptionsRejectUnknownAuthenticationScheme()
    {
        var options = ValidOptions() with
        {
            AuthenticationScheme = "test-header",
        };

        var result = new LocalIdentityOptionsValidator().Validate(null, options);

        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    public void LocalIdentityOptionsRejectInsecureOrMismatchedOrigins()
    {
        var options = ValidOptions() with
        {
            AllowedOrigins = ["http://andreja.local", "https://attacker.example"],
        };

        var result = new LocalIdentityOptionsValidator().Validate(null, options);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.Failures);
        Assert.AreEqual(2, result.Failures!.Count());
    }

    [TestMethod]
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
        Assert.IsNotNull(provider.GetRequiredService<IOptions<LocalIdentityOptions>>().Value);
        var manager = provider.GetRequiredService<UserManager<AspNetIdentityUser>>();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();
        var passkeyStateCookie = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.TwoFactorUserIdScheme);

        Assert.IsTrue(manager.SupportsUserPasskey);
        Assert.AreEqual(TimeSpan.FromMinutes(7), passkeyStateCookie.ExpireTimeSpan);
        Assert.AreEqual(
            CookieSecurePolicy.Always,
            passkeyStateCookie.Cookie.SecurePolicy);
        foreach (var scheme in schemes)
        {
            Assert.StartsWith(
                "Identity.",
                scheme.Name,
                StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void PersistenceModelHasRequiredTenantAndIdentityConstraints()
    {
        using var database = CreateDatabase(CreateContext());
        var model = database.Model;

        var externalIdentity = model.FindEntityType(typeof(ExternalIdentity));
        Assert.IsNotNull(externalIdentity);
        Assert.IsTrue(externalIdentity.GetIndexes().Any(
            index =>
                index.IsUnique
                && index.Properties.Select(property => property.Name)
                    .SequenceEqual(["Issuer", "Subject"])));

        AssertCompositeForeignKey(model.FindEntityType(typeof(Contact)), "TenantId", "LinkedPrincipalId");
        AssertCompositeForeignKey(model.FindEntityType(typeof(Membership)), "TenantId", "PrincipalId");
        AssertCompositeForeignKey(
            model.FindEntityType(typeof(AppUser)),
            "Id",
            "PrimaryExternalIdentityId");
        Assert.IsNotEmpty(model.FindEntityType(typeof(Tenant))!.GetDeclaredQueryFilters());
        Assert.IsNotEmpty(model.FindEntityType(typeof(Principal))!.GetDeclaredQueryFilters());
        Assert.IsNotEmpty(model.FindEntityType(typeof(Membership))!.GetDeclaredQueryFilters());
        Assert.IsNotEmpty(model.FindEntityType(typeof(Contact))!.GetDeclaredQueryFilters());
    }

    [TestMethod]
    public void TenantWriteFailsClosedWithoutResolvedContext()
    {
        using var database = CreateDatabase(null);
        database.Tenants.Add(
            new Tenant(TenantId.New(), "TENANT", "Tenant", "local"));

        Assert.ThrowsExactly<IdentityAccessDeniedException>(() => database.SaveChanges());
    }

    [TestMethod]
    public void TenantWriteRejectsMismatchedTenant()
    {
        var context = CreateContext();
        using var database = CreateDatabase(context);
        database.Contacts.Add(
            new Contact(ContactId.New(), TenantId.New(), "CONTACT", "Contact"));

        Assert.ThrowsExactly<IdentityAccessDeniedException>(() => database.SaveChanges());
    }

    private static void AssertCompositeForeignKey(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entity,
        params string[] propertyNames)
    {
        Assert.IsNotNull(entity);
        Assert.IsTrue(entity.GetForeignKeys().Any(
            key => key.Properties.Select(property => property.Name).SequenceEqual(propertyNames)));
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
