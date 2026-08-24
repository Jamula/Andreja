using Andreja.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Andreja.Adapters.PostgreSql;

public sealed class AndrejaIdentityDbContext(
    DbContextOptions<AndrejaIdentityDbContext> options,
    ITenantPrincipalContextAccessor contextAccessor)
    : IdentityDbContext<AspNetIdentityUser, IdentityRole<Guid>, Guid>(options)
{
    internal const string IdentitySchema = "identity";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Principal> Principals => Set<Principal>();

    public DbSet<Contact> Contacts => Set<Contact>();

    private TenantId CurrentTenantId =>
        contextAccessor.Current?.TenantId ?? new TenantId(Guid.Empty);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(IdentitySchema);

        ConfigureAspNetCoreIdentity(builder);
        ConfigureTenants(builder);
        ConfigureUsers(builder);
        ConfigureExternalIdentities(builder);
        ConfigurePrincipals(builder);
        ConfigureMemberships(builder);
        ConfigureContacts(builder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTenantWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static void ConfigureAspNetCoreIdentity(ModelBuilder builder)
    {
        builder.Entity<AspNetIdentityUser>().ToTable("credential_users");
        builder.Entity<AspNetIdentityUser>()
            .Property(user => user.AppUserId)
            .HasConversion(id => id.Value, value => new AppUserId(value));
        builder.Entity<AspNetIdentityUser>()
            .HasIndex(user => user.AppUserId)
            .IsUnique();
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityUserPasskey<Guid>>().ToTable("user_passkeys");

        builder.Entity<AspNetIdentityUser>()
            .HasOne<AppUser>()
            .WithOne()
            .HasForeignKey<AspNetIdentityUser>(user => user.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureTenants(ModelBuilder builder)
    {
        builder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(tenant => tenant.Id);
            entity.Property(tenant => tenant.Id)
                .HasConversion(id => id.Value, value => new TenantId(value))
                .ValueGeneratedNever();
            entity.Property(tenant => tenant.NormalizedName).HasMaxLength(128);
            entity.Property(tenant => tenant.DisplayName).HasMaxLength(200);
            entity.Property(tenant => tenant.DataResidency).HasMaxLength(64);
            entity.Property(tenant => tenant.Plan).HasMaxLength(64);
            entity.HasIndex(tenant => tenant.NormalizedName).IsUnique();
            entity.HasQueryFilter(tenant => tenant.Id == CurrentTenantId);
        });
    }

    private void ConfigureUsers(ModelBuilder builder)
    {
        builder.Entity<AppUser>(entity =>
        {
            entity.ToTable("app_users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id)
                .HasConversion(id => id.Value, value => new AppUserId(value))
                .ValueGeneratedNever();
            entity.Property(user => user.PrimaryExternalIdentityId)
                .HasConversion(
                    id => id.HasValue ? id.Value.Value : (Guid?)null,
                    value => value.HasValue ? new ExternalIdentityId(value.Value) : null);
            entity.Property(user => user.DisplayName).HasMaxLength(200);
            entity.HasQueryFilter(
                user => Memberships.Any(
                    membership =>
                        membership.TenantId == CurrentTenantId
                        && membership.AppUserId == user.Id));
        });
    }

    private void ConfigureExternalIdentities(ModelBuilder builder)
    {
        builder.Entity<ExternalIdentity>(entity =>
        {
            entity.ToTable("external_identities");
            entity.HasKey(identity => identity.Id);
            entity.Property(identity => identity.Id)
                .HasConversion(id => id.Value, value => new ExternalIdentityId(value))
                .ValueGeneratedNever();
            entity.Property(identity => identity.AppUserId)
                .HasConversion(id => id.Value, value => new AppUserId(value));
            entity.Property(identity => identity.Issuer).HasMaxLength(512);
            entity.Property(identity => identity.Subject).HasMaxLength(512);
            entity.HasAlternateKey(identity => new { identity.AppUserId, identity.Id });
            entity.HasIndex(identity => new { identity.Issuer, identity.Subject }).IsUnique();
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(identity => identity.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(
                identity => Memberships.Any(
                    membership =>
                        membership.TenantId == CurrentTenantId
                        && membership.AppUserId == identity.AppUserId));
        });

        builder.Entity<AppUser>()
            .HasOne<ExternalIdentity>()
            .WithMany()
            .HasForeignKey(user => new { user.Id, user.PrimaryExternalIdentityId })
            .HasPrincipalKey(identity => new { identity.AppUserId, identity.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigurePrincipals(ModelBuilder builder)
    {
        builder.Entity<Principal>(entity =>
        {
            entity.ToTable("principals");
            entity.HasKey(principal => principal.Id);
            entity.Property(principal => principal.Id)
                .HasConversion(id => id.Value, value => new PrincipalId(value))
                .ValueGeneratedNever();
            entity.Property(principal => principal.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value));
            entity.Property(principal => principal.AppUserId)
                .HasConversion(id => id.Value, value => new AppUserId(value));
            entity.Property(principal => principal.DisplayName).HasMaxLength(200);
            entity.HasAlternateKey(principal => new { principal.TenantId, principal.Id });
            entity.HasIndex(principal => new { principal.TenantId, principal.AppUserId }).IsUnique();
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(principal => principal.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(principal => principal.AppUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(principal => principal.TenantId == CurrentTenantId);
        });
    }

    private void ConfigureMemberships(ModelBuilder builder)
    {
        builder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(membership => membership.Id);
            entity.Property(membership => membership.Id)
                .HasConversion(id => id.Value, value => new MembershipId(value))
                .ValueGeneratedNever();
            entity.Property(membership => membership.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value));
            entity.Property(membership => membership.AppUserId)
                .HasConversion(id => id.Value, value => new AppUserId(value));
            entity.Property(membership => membership.PrincipalId)
                .HasConversion(id => id.Value, value => new PrincipalId(value));
            entity.HasAlternateKey(membership => new { membership.TenantId, membership.Id });
            entity.HasIndex(membership => new { membership.TenantId, membership.AppUserId }).IsUnique();
            entity.HasIndex(membership => new { membership.TenantId, membership.PrincipalId }).IsUnique();
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(membership => membership.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(membership => membership.AppUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Principal>()
                .WithOne()
                .HasForeignKey<Membership>(
                    membership => new { membership.TenantId, membership.PrincipalId })
                .HasPrincipalKey<Principal>(
                    principal => new { principal.TenantId, principal.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(membership => membership.TenantId == CurrentTenantId);
        });
    }

    private void ConfigureContacts(ModelBuilder builder)
    {
        builder.Entity<Contact>(entity =>
        {
            entity.ToTable("contacts");
            entity.HasKey(contact => contact.Id);
            entity.Property(contact => contact.Id)
                .HasConversion(id => id.Value, value => new ContactId(value))
                .ValueGeneratedNever();
            entity.Property(contact => contact.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value));
            entity.Property(contact => contact.LinkedPrincipalId)
                .HasConversion(
                    id => id.HasValue ? id.Value.Value : (Guid?)null,
                    value => value.HasValue ? new PrincipalId(value.Value) : null);
            entity.Property(contact => contact.NormalizedName).HasMaxLength(200);
            entity.Property(contact => contact.DisplayName).HasMaxLength(200);
            entity.HasAlternateKey(contact => new { contact.TenantId, contact.Id });
            entity.HasIndex(contact => new { contact.TenantId, contact.NormalizedName }).IsUnique();
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(contact => contact.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Principal>()
                .WithMany()
                .HasForeignKey(contact => new { contact.TenantId, contact.LinkedPrincipalId })
                .HasPrincipalKey(principal => new { principal.TenantId, principal.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(contact => contact.TenantId == CurrentTenantId);
        });
    }

    private void ValidateTenantWrites()
    {
        var current = TenantPrincipalContext.Require(contextAccessor);

        foreach (var entry in ChangeTracker.Entries()
                     .Where(entry =>
                         entry.State is EntityState.Added
                             or EntityState.Modified
                             or EntityState.Deleted))
        {
            var tenantId = entry.Entity switch
            {
                Tenant tenant => tenant.Id,
                Membership membership => membership.TenantId,
                Principal principal => principal.TenantId,
                Contact contact => contact.TenantId,
                _ => (TenantId?)null,
            };

            if (tenantId.HasValue && tenantId.Value != current.TenantId)
            {
                throw new IdentityAccessDeniedException(
                    "A write attempted to cross the resolved tenant boundary.");
            }
        }
    }
}
