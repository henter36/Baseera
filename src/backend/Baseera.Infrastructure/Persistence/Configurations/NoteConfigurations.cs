namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Severity);
        builder.HasIndex(x => x.DueAtUtc);
        builder.HasIndex(x => x.RegionId);
        builder.HasIndex(x => x.FacilityId);
        builder.HasIndex(x => x.FacilityUnitId);
        builder.HasIndex(x => x.OwnerDepartmentId);
        builder.HasIndex(x => x.ReportedByUserId);
        builder.HasIndex(x => x.CreatedAtUtc);

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
