namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class NoteTypeConfiguration : IEntityTypeConfiguration<NoteType>
{
    public void Configure(EntityTypeBuilder<NoteType> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DescriptionAr).HasMaxLength(1000);
        builder.Property(x => x.EntryInstructionsAr).HasMaxLength(2000);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.SortOrder);
        builder.ToTable("NoteTypes", t =>
        {
            t.HasCheckConstraint("CK_NoteTypes_SortOrder_NonNegative", "[SortOrder] >= 0");
            t.HasCheckConstraint("CK_NoteTypes_DefaultDueDays_NonNegative", "[DefaultDueDays] IS NULL OR [DefaultDueDays] >= 0");
        });
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class RoleNoteTypeGrantConfiguration : IEntityTypeConfiguration<RoleNoteTypeGrant>
{
    public void Configure(EntityTypeBuilder<RoleNoteTypeGrant> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.RoleId, x.NoteTypeId }).IsUnique();
        builder.HasIndex(x => x.NoteTypeId);
        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.NoteType).WithMany(x => x.RoleGrants).HasForeignKey(x => x.NoteTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class UserNoteTypeOverrideConfiguration : IEntityTypeConfiguration<UserNoteTypeOverride>
{
    public void Configure(EntityTypeBuilder<UserNoteTypeOverride> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.NoteTypeId }).IsUnique();
        builder.HasIndex(x => x.NoteTypeId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.NoteType).WithMany(x => x.UserOverrides).HasForeignKey(x => x.NoteTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class UserNoteIntakeProfileConfiguration : IEntityTypeConfiguration<UserNoteIntakeProfile>
{
    public void Configure(EntityTypeBuilder<UserNoteIntakeProfile> builder)
    {
        builder.ToTable("UserNoteIntakeProfiles", t =>
        {
            t.HasCheckConstraint(
                "CK_UserNoteIntakeProfiles_None_NoIds",
                "([LockType] <> 0) OR ([RegionId] IS NULL AND [FacilityId] IS NULL)");
            t.HasCheckConstraint(
                "CK_UserNoteIntakeProfiles_Region_RequiresRegion",
                "([LockType] <> 1) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL)");
            t.HasCheckConstraint(
                "CK_UserNoteIntakeProfiles_Facility_RequiresFacility",
                "([LockType] <> 2) OR ([FacilityId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.RegionId);
        builder.HasIndex(x => x.FacilityId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class NoteRoutingRuleConfiguration : IEntityTypeConfiguration<NoteRoutingRule>
{
    public void Configure(EntityTypeBuilder<NoteRoutingRule> builder)
    {
        builder.ToTable("NoteRoutingRules", t =>
        {
            t.HasCheckConstraint(
                "CK_NoteRoutingRules_GlobalHq_NoIds",
                "([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_NoteRoutingRules_Region_RequiresRegion",
                "([ScopeType] <> 2) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_NoteRoutingRules_Facility_RequiresFacility",
                "([ScopeType] <> 3) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_NoteRoutingRules_Unit_RequiresFacilityAndUnit",
                "([ScopeType] <> 4) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)");
            t.HasCheckConstraint(
                "CK_NoteRoutingRules_DepartmentTarget",
                "([ProcessingTargetType] <> 0) OR ([ProcessingDepartmentId] IS NOT NULL AND [ProcessingRoleId] IS NULL)");
            t.HasCheckConstraint(
                "CK_NoteRoutingRules_RoleTarget",
                "([ProcessingTargetType] <> 1) OR ([ProcessingRoleId] IS NOT NULL AND [ProcessingDepartmentId] IS NULL)");
            t.HasCheckConstraint("CK_NoteRoutingRules_DefaultDueDays_NonNegative", "[DefaultDueDays] IS NULL OR [DefaultDueDays] >= 0");
            t.HasCheckConstraint("CK_NoteRoutingRules_Priority_NonNegative", "[Priority] >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DescriptionAr).HasMaxLength(1000);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new
        {
            x.NoteTypeId,
            x.ScopeType,
            x.RegionId,
            x.FacilityId,
            x.FacilityUnitId,
            x.Priority,
            x.IsDeleted
        }).IsUnique();
        builder.HasIndex(x => new { x.NoteTypeId, x.IsActive, x.IsDeleted });
        builder.HasIndex(x => new { x.ScopeType, x.RegionId, x.FacilityId, x.FacilityUnitId });

        builder.HasOne(x => x.NoteType).WithMany().HasForeignKey(x => x.NoteTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FacilityUnit).WithMany().HasForeignKey(x => x.FacilityUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProcessingDepartment).WithMany().HasForeignKey(x => x.ProcessingDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProcessingRole).WithMany().HasForeignKey(x => x.ProcessingRoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewerRole).WithMany().HasForeignKey(x => x.ReviewerRoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActivatedByUser).WithMany().HasForeignKey(x => x.ActivatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeactivatedByUser).WithMany().HasForeignKey(x => x.DeactivatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class NoteRoutingDecisionConfiguration : IEntityTypeConfiguration<NoteRoutingDecision>
{
    public void Configure(EntityTypeBuilder<NoteRoutingDecision> builder)
    {
        builder.ToTable("NoteRoutingDecisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DecisionKey).HasMaxLength(240).IsRequired();
        builder.Property(x => x.DueAtSource).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.Property(x => x.FailureCode).HasMaxLength(100);
        builder.Property(x => x.FailureMessageSafe).HasMaxLength(1000);
        builder.Property(x => x.MetadataJson).HasMaxLength(4000);
        builder.HasIndex(x => x.DecisionKey).IsUnique();
        builder.HasIndex(x => new { x.OperationalNoteId, x.DecidedAtUtc });
        builder.HasIndex(x => new { x.RoutingRuleId, x.ResultStatus });
        builder.HasOne(x => x.OperationalNote).WithMany(n => n.RoutingDecisions).HasForeignKey(x => x.OperationalNoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RoutingRule).WithMany(r => r.Decisions).HasForeignKey(x => x.RoutingRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResolvedDepartment).WithMany().HasForeignKey(x => x.ResolvedDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResolvedUser).WithMany().HasForeignKey(x => x.ResolvedUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResolvedProcessingRole).WithMany().HasForeignKey(x => x.ResolvedProcessingRoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResolvedReviewerRole).WithMany().HasForeignKey(x => x.ResolvedReviewerRoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DecidedByUser).WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NoteRoutingRuleHistoryConfiguration : IEntityTypeConfiguration<NoteRoutingRuleHistory>
{
    public void Configure(EntityTypeBuilder<NoteRoutingRuleHistory> builder)
    {
        builder.ToTable("NoteRoutingRuleHistories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SnapshotJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.HasIndex(x => new { x.RoutingRuleId, x.ChangedAtUtc });
        builder.HasOne(x => x.RoutingRule).WithMany(r => r.History).HasForeignKey(x => x.RoutingRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NoteTypeAccessChangeHistoryConfiguration : IEntityTypeConfiguration<NoteTypeAccessChangeHistory>
{
    public void Configure(EntityTypeBuilder<NoteTypeAccessChangeHistory> builder)
    {
        builder.ToTable("NoteTypeAccessChangeHistories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PreviousCapabilitiesJson).HasMaxLength(4000);
        builder.Property(x => x.NewCapabilitiesJson).HasMaxLength(4000);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.HasIndex(x => new { x.PrincipalType, x.PrincipalId, x.ChangedAtUtc });
        builder.HasIndex(x => new { x.NoteTypeId, x.ChangedAtUtc });
        builder.HasOne(x => x.NoteType).WithMany().HasForeignKey(x => x.NoteTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OperationalNoteConfiguration : IEntityTypeConfiguration<OperationalNote>
{
    public void Configure(EntityTypeBuilder<OperationalNote> builder)
    {
        builder.ToTable("OperationalNotes", t =>
        {
            t.HasCheckConstraint(
                "CK_OperationalNotes_GlobalHq_NoIds",
                "([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_OperationalNotes_Region_RequiresRegion",
                "([ScopeType] <> 2) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_OperationalNotes_Facility_RequiresFacility",
                "([ScopeType] <> 3) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_OperationalNotes_Unit_RequiresFacilityAndUnit",
                "([ScopeType] <> 4) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)");
            t.HasCheckConstraint(
                "CK_OperationalNotes_SupportedScopes",
                "[ScopeType] IN (0, 1, 2, 3, 4)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.SourceReference).HasMaxLength(200);
        builder.Property(x => x.ClosureSummary).HasMaxLength(2000);
        builder.Property(x => x.ReopenReason).HasMaxLength(2000);

        builder.HasIndex(x => x.ReferenceNumber).IsUnique();
        builder.HasIndex(x => x.NoteTypeId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Severity);
        builder.HasIndex(x => x.DueAtUtc);
        builder.HasIndex(x => x.RegionId);
        builder.HasIndex(x => x.FacilityId);
        builder.HasIndex(x => x.FacilityUnitId);
        builder.HasIndex(x => x.OwnerDepartmentId);
        builder.HasIndex(x => x.ReportedByUserId);
        builder.HasIndex(x => x.CreatedAtUtc);

        builder.HasOne(x => x.NoteType).WithMany(t => t.OperationalNotes).HasForeignKey(x => x.NoteTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FacilityUnit).WithMany().HasForeignKey(x => x.FacilityUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OwnerDepartment).WithMany().HasForeignKey(x => x.OwnerDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReportedByUser).WithMany().HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ClosedByUser).WithMany().HasForeignKey(x => x.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReopenedByUser).WithMany().HasForeignKey(x => x.ReopenedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureRowVersion();
    }
}

internal sealed class NoteAssignmentConfiguration : IEntityTypeConfiguration<NoteAssignment>
{
    public void Configure(EntityTypeBuilder<NoteAssignment> builder)
    {
        builder.ToTable("NoteAssignments", t =>
        {
            t.HasCheckConstraint(
                "CK_NoteAssignments_UserXorDepartment",
                "([AssignedToUserId] IS NOT NULL AND [AssignedToDepartmentId] IS NULL) OR ([AssignedToUserId] IS NULL AND [AssignedToDepartmentId] IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.EndReason).HasMaxLength(2000);

        builder.HasIndex(x => x.OperationalNoteId)
            .IsUnique()
            .HasFilter("[IsCurrent] = 1");

        builder.HasOne(x => x.OperationalNote).WithMany(n => n.Assignments).HasForeignKey(x => x.OperationalNoteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedToDepartment).WithMany().HasForeignKey(x => x.AssignedToDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedByUser).WithMany().HasForeignKey(x => x.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RoutingDecision).WithMany().HasForeignKey(x => x.RoutingDecisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureRowVersion();
    }
}

internal sealed class NoteStatusHistoryConfiguration : IEntityTypeConfiguration<NoteStatusHistory>
{
    public void Configure(EntityTypeBuilder<NoteStatusHistory> builder)
    {
        builder.ToTable("NoteStatusHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.MetadataJson).HasMaxLength(4000);

        builder.HasIndex(x => new { x.OperationalNoteId, x.ChangedAtUtc });

        builder.HasOne(x => x.OperationalNote).WithMany(n => n.StatusHistory).HasForeignKey(x => x.OperationalNoteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Assignment).WithMany().HasForeignKey(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
