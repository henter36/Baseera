namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class FormDefinitionConfiguration : IEntityTypeConfiguration<FormDefinition>
{
    public void Configure(EntityTypeBuilder<FormDefinition> builder)
    {
        builder.ToTable("FormDefinitions", t =>
        {
            t.HasCheckConstraint(
                "CK_FormDefinitions_GlobalHq_NoIds",
                "([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_FormDefinitions_Region_RequiresRegion",
                "([ScopeType] <> 2) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_FormDefinitions_Facility_RequiresFacility",
                "([ScopeType] <> 3) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_FormDefinitions_Unit_RequiresUnit",
                "([ScopeType] <> 4) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Classification);
        builder.HasIndex(x => x.OwnerDepartmentId);
        builder.HasIndex(x => x.RegionId);
        builder.HasIndex(x => x.FacilityId);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.IsDeleted);

        builder.HasOne(x => x.OwnerDepartment).WithMany().HasForeignKey(x => x.OwnerDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FacilityUnit).WithMany().HasForeignKey(x => x.FacilityUnitId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ArchivedByUser).WithMany().HasForeignKey(x => x.ArchivedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeletedByUser).WithMany().HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LastModifiedByUser).WithMany().HasForeignKey(x => x.LastModifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormReviewDecisionConfiguration : IEntityTypeConfiguration<FormReviewDecision>
{
    public void Configure(EntityTypeBuilder<FormReviewDecision> builder)
    {
        builder.ToTable("FormReviewDecisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.HasIndex(x => x.FormDefinitionId);
        builder.HasIndex(x => x.ReviewedAtUtc);
        builder.HasOne(x => x.FormDefinition).WithMany(f => f.ReviewDecisions).HasForeignKey(x => x.FormDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FormGovernancePolicyConfiguration : IEntityTypeConfiguration<FormGovernancePolicy>
{
    public void Configure(EntityTypeBuilder<FormGovernancePolicy> builder)
    {
        builder.ToTable("FormGovernancePolicies");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormAccessGrantConfiguration : IEntityTypeConfiguration<FormAccessGrant>
{
    public void Configure(EntityTypeBuilder<FormAccessGrant> builder)
    {
        builder.ToTable("FormAccessGrants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => new { x.FormDefinitionId, x.PrincipalType, x.PrincipalId, x.Capability, x.Effect })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.ValidToUtc);
        builder.HasOne(x => x.FormDefinition).WithMany(f => f.AccessGrants).HasForeignKey(x => x.FormDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RevokedByUser).WithMany().HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}
