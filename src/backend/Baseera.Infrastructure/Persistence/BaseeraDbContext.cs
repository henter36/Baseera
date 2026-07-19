namespace Baseera.Infrastructure.Persistence;

using Baseera.Domain.Attachments;
using Baseera.Domain.Audit;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class BaseeraDbContext(DbContextOptions<BaseeraDbContext> options) : DbContext(options), Application.Abstractions.IBaseeraDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<FacilityUnit> FacilityUnits => Set<FacilityUnit>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<FacilityAssetLocation> FacilityAssetLocations => Set<FacilityAssetLocation>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserScope> UserScopes => Set<UserScope>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    IQueryable<Organization> Application.Abstractions.IBaseeraDbContext.Organizations => Organizations;
    IQueryable<Region> Application.Abstractions.IBaseeraDbContext.Regions => Regions;
    IQueryable<Facility> Application.Abstractions.IBaseeraDbContext.Facilities => Facilities;
    IQueryable<FacilityUnit> Application.Abstractions.IBaseeraDbContext.FacilityUnits => FacilityUnits;
    IQueryable<Building> Application.Abstractions.IBaseeraDbContext.Buildings => Buildings;
    IQueryable<FacilityAssetLocation> Application.Abstractions.IBaseeraDbContext.FacilityAssetLocations => FacilityAssetLocations;
    IQueryable<Department> Application.Abstractions.IBaseeraDbContext.Departments => Departments;
    IQueryable<User> Application.Abstractions.IBaseeraDbContext.Users => Users;
    IQueryable<Role> Application.Abstractions.IBaseeraDbContext.Roles => Roles;
    IQueryable<Permission> Application.Abstractions.IBaseeraDbContext.Permissions => Permissions;
    IQueryable<UserRole> Application.Abstractions.IBaseeraDbContext.UserRoles => UserRoles;
    IQueryable<RolePermission> Application.Abstractions.IBaseeraDbContext.RolePermissions => RolePermissions;
    IQueryable<UserScope> Application.Abstractions.IBaseeraDbContext.UserScopes => UserScopes;
    IQueryable<AuditLog> Application.Abstractions.IBaseeraDbContext.AuditLogs => AuditLogs;
    IQueryable<Attachment> Application.Abstractions.IBaseeraDbContext.Attachments => Attachments;

    public new void Add<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Add(entity);
    public new void Update<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Update(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BaseeraDbContext).Assembly);

        modelBuilder.Entity<Organization>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Region>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Facility>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<FacilityUnit>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Building>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<FacilityAssetLocation>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Department>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Role>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<UserScope>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Attachment>().HasQueryFilter(e => !e.IsDeleted);

        modelBuilder.Entity<UserScope>().ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_UserScopes_GlobalHq_NoIds",
                "([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_UserScopes_Region_RequiresRegion",
                "([ScopeType] NOT IN (2, 5)) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_UserScopes_Facility_RequiresFacility",
                "([ScopeType] NOT IN (3, 6)) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_UserScopes_Unit_RequiresFacilityAndUnit",
                "([ScopeType] <> 4) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)");
        });
    }

    public override int SaveChanges()
    {
        EnforceAuditImmutability();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceAuditImmutability();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void EnforceAuditImmutability()
    {
        foreach (var entry in ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("AuditLog is append-only and cannot be modified or deleted.");
            }
        }
    }
}

public sealed class AuditImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Guard(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("AuditLog is append-only and cannot be modified or deleted.");
            }
        }
    }
}
