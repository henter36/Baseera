namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.Attachments;
using Baseera.Domain.Audit;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal static class ConcurrencyExtensions
{
    public static void ConfigureRowVersion<T>(this EntityTypeBuilder<T> builder) where T : EntityBase =>
        builder.Property(e => e.RowVersion).IsRowVersion();
}

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.ConfigureRowVersion();
    }
}

internal sealed class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("Regions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasOne(x => x.Organization).WithMany(o => o.Regions).HasForeignKey(x => x.OrganizationId);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.ToTable("Facilities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasOne(x => x.Region).WithMany(r => r.Facilities).HasForeignKey(x => x.RegionId);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FacilityUnitConfiguration : IEntityTypeConfiguration<FacilityUnit>
{
    public void Configure(EntityTypeBuilder<FacilityUnit> builder)
    {
        builder.ToTable("FacilityUnits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.FacilityId, x.Code }).IsUnique();
        builder.HasOne(x => x.Facility).WithMany(f => f.Units).HasForeignKey(x => x.FacilityId);
        builder.HasOne(x => x.ParentUnit).WithMany(p => p.Children).HasForeignKey(x => x.ParentUnitId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("Buildings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Facility).WithMany(f => f.Buildings).HasForeignKey(x => x.FacilityId);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FacilityAssetLocationConfiguration : IEntityTypeConfiguration<FacilityAssetLocation>
{
    public void Configure(EntityTypeBuilder<FacilityAssetLocation> builder)
    {
        builder.ToTable("FacilityAssetLocations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Building).WithMany(b => b.Locations).HasForeignKey(x => x.BuildingId);
        builder.ConfigureRowVersion();
    }
}

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Organization).WithMany(o => o.Departments).HasForeignKey(x => x.OrganizationId);
        builder.HasOne(x => x.ParentDepartment).WithMany(p => p.Children).HasForeignKey(x => x.ParentDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalSubject).HasMaxLength(128).IsRequired();
        builder.Property(x => x.UserName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DisplayNameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasIndex(x => x.ExternalSubject).IsUnique();
        builder.HasIndex(x => x.UserName).IsUnique();
        builder.ConfigureRowVersion();
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.ConfigureRowVersion();
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.ConfigureRowVersion();
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(x => new { x.RoleId, x.PermissionId });
        builder.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId);
        builder.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId);
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(x => new { x.UserId, x.RoleId });
        builder.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId);
    }
}

internal sealed class UserScopeConfiguration : IEntityTypeConfiguration<UserScope>
{
    public void Configure(EntityTypeBuilder<UserScope> builder)
    {
        builder.ToTable("UserScopes");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.User).WithMany(u => u.UserScopes).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FacilityUnit).WithMany().HasForeignKey(x => x.FacilityUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.Module, x.EntityType, x.EntityId });
    }
}

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.StoredFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StoragePath).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.Sha256);
        builder.ConfigureRowVersion();
    }
}
