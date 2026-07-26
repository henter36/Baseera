namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class WorkforceMemberConfiguration : IEntityTypeConfiguration<WorkforceMember>
{
    public void Configure(EntityTypeBuilder<WorkforceMember> builder)
    {
        builder.ToTable("WorkforceMembers", table =>
        {
            table.HasCheckConstraint(
                "CK_WorkforceMembers_NoSelfSupervision",
                "[SupervisorWorkforceMemberId] IS NULL OR [SupervisorWorkforceMemberId] <> [Id]");
            table.HasCheckConstraint(
                "CK_WorkforceMembers_UnitRequiresFacility",
                "[CurrentOperationalUnitId] IS NULL OR [CurrentOperationalFacilityId] IS NOT NULL");
        });
        builder.HasKey(member => member.Id);
        builder.Property(member => member.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(member => member.EmployeeNumber).HasMaxLength(80).IsRequired();
        builder.Property(member => member.ExternalPersonnelId).HasMaxLength(120);
        builder.Property(member => member.RankOrGrade).HasMaxLength(80);
        builder.Property(member => member.JobTitle).HasMaxLength(160).IsRequired();
        builder.Property(member => member.PrimarySpecialty).HasMaxLength(160).IsRequired();
        builder.Property(member => member.SourceReference).HasMaxLength(160);
        builder.HasIndex(member => new { member.OrganizationId, member.EmployeeNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(member => new { member.OrganizationId, member.ExternalPersonnelId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [ExternalPersonnelId] IS NOT NULL");
        builder.HasIndex(member => new { member.CurrentOperationalFacilityId, member.EmploymentStatus });
        builder.HasOne(member => member.Organization).WithMany().HasForeignKey(member => member.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(member => member.AdministrativeOrganization).WithMany().HasForeignKey(member => member.AdministrativeOrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(member => member.User).WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(member => member.HomeFacility).WithMany().HasForeignKey(member => member.HomeFacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(member => member.CurrentOperationalFacility).WithMany().HasForeignKey(member => member.CurrentOperationalFacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(member => member.CurrentOperationalUnit)
            .WithMany()
            .HasForeignKey(member => new { member.CurrentOperationalFacilityId, member.CurrentOperationalUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(member => member.SupervisorWorkforceMember)
            .WithMany()
            .HasForeignKey(member => member.SupervisorWorkforceMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class WorkforceRoleDefinitionConfiguration : IEntityTypeConfiguration<WorkforceRoleDefinition>
{
    public void Configure(EntityTypeBuilder<WorkforceRoleDefinition> builder)
    {
        builder.ToTable("WorkforceRoleDefinitions");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Code).HasMaxLength(80).IsRequired();
        builder.Property(role => role.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(role => role.NameEn).HasMaxLength(200);
        builder.HasIndex(role => new { role.OrganizationId, role.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasOne(role => role.Organization).WithMany().HasForeignKey(role => role.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class WorkforceQualificationConfiguration : IEntityTypeConfiguration<WorkforceQualification>
{
    public void Configure(EntityTypeBuilder<WorkforceQualification> builder)
    {
        builder.ToTable("WorkforceQualifications");
        builder.HasKey(qualification => qualification.Id);
        builder.Property(qualification => qualification.Name).HasMaxLength(200).IsRequired();
        builder.Property(qualification => qualification.Issuer).HasMaxLength(160);
        builder.Property(qualification => qualification.Reference).HasMaxLength(160);
        builder.Property(qualification => qualification.VerifiedBy).HasMaxLength(120);
        builder.HasIndex(qualification => new { qualification.WorkforceMemberId, qualification.QualificationType, qualification.Status });
        builder.HasOne(qualification => qualification.WorkforceMember)
            .WithMany(member => member.Qualifications)
            .HasForeignKey(qualification => qualification.WorkforceMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(qualification => qualification.RoleDefinition).WithMany().HasForeignKey(qualification => qualification.RoleDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class WorkforceAssignmentConfiguration : IEntityTypeConfiguration<WorkforceAssignment>
{
    public void Configure(EntityTypeBuilder<WorkforceAssignment> builder)
    {
        builder.ToTable("WorkforceAssignments", table =>
        {
            table.HasCheckConstraint(
                "CK_WorkforceAssignments_EffectiveRange",
                "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
            table.HasCheckConstraint(
                "CK_WorkforceAssignments_UnitRequiresFacility",
                "[FacilityUnitId] IS NULL OR [FacilityId] IS NOT NULL");
        });
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.SourceReference).HasMaxLength(160);
        builder.Property(assignment => assignment.Reason).HasMaxLength(1000);
        builder.Property(assignment => assignment.ApprovedBy).HasMaxLength(120);
        builder.HasIndex(assignment => new { assignment.FacilityId, assignment.FacilityUnitId, assignment.RoleDefinitionId, assignment.EffectiveFromUtc });
        builder.HasIndex(assignment => new { assignment.WorkforceMemberId, assignment.IsPrimary, assignment.EffectiveFromUtc });
        builder.HasOne(assignment => assignment.WorkforceMember)
            .WithMany(member => member.Assignments)
            .HasForeignKey(assignment => assignment.WorkforceMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.Facility).WithMany().HasForeignKey(assignment => assignment.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.FacilityUnit)
            .WithMany()
            .HasForeignKey(assignment => new { assignment.FacilityId, assignment.FacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.RoleDefinition).WithMany().HasForeignKey(assignment => assignment.RoleDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class StaffingRequirementConfiguration : IEntityTypeConfiguration<StaffingRequirement>
{
    public void Configure(EntityTypeBuilder<StaffingRequirement> builder)
    {
        builder.ToTable("StaffingRequirements", table =>
        {
            table.HasCheckConstraint(
                "CK_StaffingRequirements_Quantities",
                "[RequiredHeadcount] >= 0 AND [MinimumSafeHeadcount] >= 0 AND [MinimumSafeHeadcount] <= [RequiredHeadcount]");
            table.HasCheckConstraint(
                "CK_StaffingRequirements_EffectiveRange",
                "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
        });
        builder.HasKey(requirement => requirement.Id);
        builder.Property(requirement => requirement.SourceReference).HasMaxLength(160).IsRequired();
        builder.Property(requirement => requirement.ApprovalReference).HasMaxLength(160);
        builder.Property(requirement => requirement.Notes).HasMaxLength(1000);
        builder.HasIndex(requirement => new { requirement.FacilityId, requirement.FacilityUnitId, requirement.RoleDefinitionId, requirement.ShiftDefinitionId, requirement.EffectiveFromUtc });
        builder.HasOne(requirement => requirement.Organization).WithMany().HasForeignKey(requirement => requirement.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.Facility).WithMany().HasForeignKey(requirement => requirement.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.FacilityUnit)
            .WithMany()
            .HasForeignKey(requirement => new { requirement.FacilityId, requirement.FacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.RoleDefinition).WithMany().HasForeignKey(requirement => requirement.RoleDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.ShiftDefinition).WithMany().HasForeignKey(requirement => requirement.ShiftDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class ShiftDefinitionConfiguration : IEntityTypeConfiguration<ShiftDefinition>
{
    public void Configure(EntityTypeBuilder<ShiftDefinition> builder)
    {
        builder.ToTable("ShiftDefinitions", table =>
        {
            table.HasCheckConstraint(
                "CK_ShiftDefinitions_CrossesMidnight",
                "([CrossesMidnight] = 1 AND [EndLocalTime] <= [StartLocalTime]) OR ([CrossesMidnight] = 0 AND [EndLocalTime] > [StartLocalTime])");
        });
        builder.HasKey(shift => shift.Id);
        builder.Property(shift => shift.Code).HasMaxLength(80).IsRequired();
        builder.Property(shift => shift.Name).HasMaxLength(160).IsRequired();
        builder.Property(shift => shift.Timezone).HasMaxLength(80).IsRequired();
        builder.HasIndex(shift => new { shift.FacilityId, shift.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasOne(shift => shift.Organization).WithMany().HasForeignKey(shift => shift.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(shift => shift.Facility).WithMany().HasForeignKey(shift => shift.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class DutyRosterConfiguration : IEntityTypeConfiguration<DutyRoster>
{
    public void Configure(EntityTypeBuilder<DutyRoster> builder)
    {
        builder.ToTable("DutyRosters", table =>
        {
            table.HasCheckConstraint(
                "CK_DutyRosters_Status",
                "[Status] IN (N'Draft', N'Published')");
            table.HasCheckConstraint(
                "CK_DutyRosters_PublishedState",
                "([Status] = N'Published' AND [PublishedAtUtc] IS NOT NULL) OR ([Status] = N'Draft' AND [PublishedAtUtc] IS NULL AND [PublishedBy] IS NULL)");
        });
        builder.HasKey(roster => roster.Id);
        builder.Property(roster => roster.Status).HasMaxLength(40).IsRequired();
        builder.Property(roster => roster.PublishedBy).HasMaxLength(120);
        builder.HasIndex(roster => new { roster.FacilityId, roster.ShiftDefinitionId, roster.DutyDate })
            .IsUnique()
            .HasDatabaseName("IX_DutyRosters_FacilityShiftDate_NoUnit")
            .HasFilter("[IsDeleted] = 0 AND [FacilityUnitId] IS NULL");
        builder.HasIndex(roster => new { roster.FacilityId, roster.FacilityUnitId, roster.ShiftDefinitionId, roster.DutyDate })
            .IsUnique()
            .HasDatabaseName("IX_DutyRosters_FacilityUnitShiftDate")
            .HasFilter("[IsDeleted] = 0 AND [FacilityUnitId] IS NOT NULL");
        builder.HasOne(roster => roster.Facility).WithMany().HasForeignKey(roster => roster.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(roster => roster.FacilityUnit)
            .WithMany()
            .HasForeignKey(roster => new { roster.FacilityId, roster.FacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(roster => roster.ShiftDefinition).WithMany().HasForeignKey(roster => roster.ShiftDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class DutyRosterAssignmentConfiguration : IEntityTypeConfiguration<DutyRosterAssignment>
{
    public void Configure(EntityTypeBuilder<DutyRosterAssignment> builder)
    {
        builder.ToTable("DutyRosterAssignments");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Status).HasConversion<int>();
        builder.Property(assignment => assignment.Notes).HasMaxLength(1000);
        builder.HasIndex(assignment => new { assignment.DutyRosterId, assignment.WorkforceMemberId, assignment.RoleDefinitionId });
        builder.HasOne(assignment => assignment.DutyRoster)
            .WithMany(roster => roster.Assignments)
            .HasForeignKey(assignment => assignment.DutyRosterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.WorkforceMember).WithMany().HasForeignKey(assignment => assignment.WorkforceMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.RoleDefinition).WithMany().HasForeignKey(assignment => assignment.RoleDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.ReplacementForAssignment)
            .WithMany()
            .HasForeignKey(assignment => assignment.ReplacementForAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class WorkforceAvailabilityEventConfiguration : IEntityTypeConfiguration<WorkforceAvailabilityEvent>
{
    public void Configure(EntityTypeBuilder<WorkforceAvailabilityEvent> builder)
    {
        builder.ToTable("WorkforceAvailabilityEvents", table =>
        {
            table.HasCheckConstraint(
                "CK_WorkforceAvailabilityEvents_EffectiveRange",
                "[EndsAtUtc] IS NULL OR [EndsAtUtc] > [StartsAtUtc]");
        });
        builder.HasKey(availability => availability.Id);
        builder.Property(availability => availability.SourceReference).HasMaxLength(160);
        builder.Property(availability => availability.ReasonCode).HasMaxLength(80);
        builder.Property(availability => availability.RestrictionCodesCsv).HasMaxLength(500);
        builder.Property(availability => availability.RecordedBy).HasMaxLength(120);
        builder.HasIndex(availability => new { availability.WorkforceMemberId, availability.StartsAtUtc, availability.EndsAtUtc });
        builder.HasOne(availability => availability.WorkforceMember)
            .WithMany(member => member.AvailabilityEvents)
            .HasForeignKey(availability => availability.WorkforceMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class CriticalPositionRequirementConfiguration : IEntityTypeConfiguration<CriticalPositionRequirement>
{
    public void Configure(EntityTypeBuilder<CriticalPositionRequirement> builder)
    {
        builder.ToTable("CriticalPositionRequirements", table =>
        {
            table.HasCheckConstraint(
                "CK_CriticalPositionRequirements_Counts",
                "[RequiredPrimaryCount] >= 0 AND [RequiredAlternateCount] >= 0");
            table.HasCheckConstraint(
                "CK_CriticalPositionRequirements_EffectiveRange",
                "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
        });
        builder.HasKey(requirement => requirement.Id);
        builder.HasIndex(requirement => new { requirement.FacilityId, requirement.RoleDefinitionId, requirement.ShiftDefinitionId, requirement.EffectiveFromUtc });
        builder.HasOne(requirement => requirement.Facility).WithMany().HasForeignKey(requirement => requirement.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.FacilityUnit)
            .WithMany()
            .HasForeignKey(requirement => new { requirement.FacilityId, requirement.FacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.RoleDefinition).WithMany().HasForeignKey(requirement => requirement.RoleDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.ShiftDefinition).WithMany().HasForeignKey(requirement => requirement.ShiftDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class WorkforceReadinessSnapshotConfiguration : IEntityTypeConfiguration<WorkforceReadinessSnapshot>
{
    public void Configure(EntityTypeBuilder<WorkforceReadinessSnapshot> builder)
    {
        builder.ToTable("WorkforceReadinessSnapshots");
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Freshness).HasMaxLength(40).IsRequired();
        builder.Property(snapshot => snapshot.Confidence).HasMaxLength(40).IsRequired();
        builder.Property(snapshot => snapshot.SourceStatus).HasMaxLength(40).IsRequired();
        builder.Property(snapshot => snapshot.CoverageRate).HasPrecision(9, 4);
        builder.Property(snapshot => snapshot.QualificationCoverage).HasPrecision(9, 4);
        builder.HasIndex(snapshot => new { snapshot.FacilityId, snapshot.CapturedAtUtc });
        builder.HasIndex(snapshot => new { snapshot.FacilityId, snapshot.FacilityUnitId, snapshot.ShiftDefinitionId, snapshot.RoleDefinitionId, snapshot.CapturedAtUtc });
        builder.HasOne(snapshot => snapshot.Facility).WithMany().HasForeignKey(snapshot => snapshot.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(snapshot => snapshot.FacilityUnit)
            .WithMany()
            .HasForeignKey(snapshot => new { snapshot.FacilityId, snapshot.FacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(snapshot => snapshot.ShiftDefinition).WithMany().HasForeignKey(snapshot => snapshot.ShiftDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(snapshot => snapshot.RoleDefinition).WithMany().HasForeignKey(snapshot => snapshot.RoleDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class WorkforceImportBatchConfiguration : IEntityTypeConfiguration<WorkforceImportBatch>
{
    public void Configure(EntityTypeBuilder<WorkforceImportBatch> builder)
    {
        builder.ToTable("WorkforceImportBatches", table =>
        {
            table.HasCheckConstraint(
                "CK_WorkforceImportBatches_RowTotals",
                "[ValidRows] + [RejectedRows] + [DuplicateRows] = [TotalRows] AND [TotalRows] >= 0 AND [ValidRows] >= 0 AND [RejectedRows] >= 0 AND [DuplicateRows] >= 0");
            table.HasCheckConstraint(
                "CK_WorkforceImportBatches_AppliedRows",
                "[AppliedRows] >= 0 AND [AppliedRows] <= [ValidRows]");
            table.HasCheckConstraint(
                "CK_WorkforceImportBatches_Status",
                "[Status] IN (N'Previewed', N'Confirmed')");
            table.HasCheckConstraint(
                "CK_WorkforceImportBatches_ConfirmedState",
                "([Status] = N'Confirmed' AND [ConfirmedAtUtc] IS NOT NULL) OR ([Status] = N'Previewed' AND [AppliedRows] = 0 AND [ConfirmedAtUtc] IS NULL)");
        });
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.SourceSystem).HasMaxLength(120).IsRequired();
        builder.Property(batch => batch.SourceReference).HasMaxLength(160).IsRequired();
        builder.Property(batch => batch.FileHash).HasMaxLength(128).IsRequired();
        builder.Property(batch => batch.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(batch => new { batch.FacilityId, batch.ImportKind, batch.SourceSystem, batch.SourceReference, batch.FileHash }).IsUnique();
        builder.HasOne(batch => batch.Facility).WithMany().HasForeignKey(batch => batch.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(batch => batch.SubmittedByUser).WithMany().HasForeignKey(batch => batch.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class WorkforceReconciliationResolutionConfiguration : IEntityTypeConfiguration<WorkforceReconciliationResolution>
{
    public void Configure(EntityTypeBuilder<WorkforceReconciliationResolution> builder)
    {
        builder.ToTable("WorkforceReconciliationResolutions");
        builder.HasKey(resolution => resolution.Id);
        builder.Property(resolution => resolution.ItemKey).HasMaxLength(200).IsRequired();
        builder.Property(resolution => resolution.IssueType).HasMaxLength(80).IsRequired();
        builder.Property(resolution => resolution.ResolutionAction).HasMaxLength(80).IsRequired();
        builder.Property(resolution => resolution.Notes).HasMaxLength(1000);
        builder.Property(resolution => resolution.ResolvedBy).HasMaxLength(120);
        builder.HasIndex(resolution => new { resolution.FacilityId, resolution.ItemKey }).IsUnique();
        builder.HasOne(resolution => resolution.Facility).WithMany().HasForeignKey(resolution => resolution.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}
