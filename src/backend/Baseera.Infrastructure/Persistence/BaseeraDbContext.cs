namespace Baseera.Infrastructure.Persistence;

using Baseera.Application.Abstractions;
using Baseera.Domain.Attachments;
using Baseera.Domain.Audit;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Microsoft.EntityFrameworkCore;

public sealed class BaseeraDbContext(DbContextOptions<BaseeraDbContext> options) : DbContext(options), IBaseeraDbContext
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

    IQueryable<Organization> IBaseeraDbContext.Organizations => Organizations;
    IQueryable<Region> IBaseeraDbContext.Regions => Regions;
    IQueryable<Facility> IBaseeraDbContext.Facilities => Facilities;
    IQueryable<FacilityUnit> IBaseeraDbContext.FacilityUnits => FacilityUnits;
    IQueryable<Building> IBaseeraDbContext.Buildings => Buildings;
    IQueryable<FacilityAssetLocation> IBaseeraDbContext.FacilityAssetLocations => FacilityAssetLocations;
    IQueryable<Department> IBaseeraDbContext.Departments => Departments;
    IQueryable<User> IBaseeraDbContext.Users => Users;
    IQueryable<Role> IBaseeraDbContext.Roles => Roles;
    IQueryable<Permission> IBaseeraDbContext.Permissions => Permissions;
    IQueryable<UserRole> IBaseeraDbContext.UserRoles => UserRoles;
    IQueryable<RolePermission> IBaseeraDbContext.RolePermissions => RolePermissions;
    IQueryable<UserScope> IBaseeraDbContext.UserScopes => UserScopes;
    IQueryable<AuditLog> IBaseeraDbContext.AuditLogs => AuditLogs;
    IQueryable<Attachment> IBaseeraDbContext.Attachments => Attachments;

    public new void Add<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Add(entity);
    public new void Update<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Update(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BaseeraDbContext).Assembly);

        // Soft-delete filters are applied explicitly in application queries.
        // Global filters can be added per-entity in later phases once all modules are migrated.
    }
}
