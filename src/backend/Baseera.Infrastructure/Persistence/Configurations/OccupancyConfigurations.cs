namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.Occupancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class FacilityCapacityBaselineConfiguration : IEntityTypeConfiguration<FacilityCapacityBaseline>
{
    public void Configure(EntityTypeBuilder<FacilityCapacityBaseline> builder)
    {
        builder.ToTable("FacilityCapacityBaselines", t =>
        {
            t.HasCheckConstraint("CK_FacilityCapacityBaselines_Capacity_Positive", "[ApprovedCapacity] > 0");
            t.HasCheckConstraint("CK_FacilityCapacityBaselines_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ApprovalReference).HasMaxLength(120);
        builder.Property(x => x.SourceReference).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.FacilityId, x.FacilityUnitId, x.CapacityType, x.EffectiveFromUtc });
        builder.HasIndex(x => new { x.FacilityId, x.IsDeleted });
        builder.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FacilityUnit).WithMany().HasForeignKey(x => x.FacilityUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class InmateCensusSnapshotConfiguration : IEntityTypeConfiguration<InmateCensusSnapshot>
{
    public void Configure(EntityTypeBuilder<InmateCensusSnapshot> builder)
    {
        builder.ToTable("InmateCensusSnapshots", t =>
        {
            t.HasCheckConstraint("CK_InmateCensusSnapshots_Count_NonNegative", "[InmateCount] >= 0");
            t.HasCheckConstraint("CK_InmateCensusSnapshots_Male_NonNegative", "[MaleCount] IS NULL OR [MaleCount] >= 0");
            t.HasCheckConstraint("CK_InmateCensusSnapshots_Female_NonNegative", "[FemaleCount] IS NULL OR [FemaleCount] >= 0");
            t.HasCheckConstraint("CK_InmateCensusSnapshots_Adult_NonNegative", "[AdultCount] IS NULL OR [AdultCount] >= 0");
            t.HasCheckConstraint("CK_InmateCensusSnapshots_Juvenile_NonNegative", "[JuvenileCount] IS NULL OR [JuvenileCount] >= 0");
            t.HasCheckConstraint("CK_InmateCensusSnapshots_Medical_NonNegative", "[MedicalCount] IS NULL OR [MedicalCount] >= 0");
            t.HasCheckConstraint("CK_InmateCensusSnapshots_Isolation_NonNegative", "[IsolationCount] IS NULL OR [IsolationCount] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceReference).HasMaxLength(160).IsRequired();
        builder.Property(x => x.SourceVersion).HasMaxLength(80);
        builder.Property(x => x.QualityNotes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.FacilityId, x.FacilityUnitId, x.CapturedAtUtc });
        builder.HasIndex(x => new { x.FacilityId, x.IsAuthoritative, x.CapturedAtUtc });
        builder.HasIndex(x => new { x.FacilityId, x.IsDeleted });
        builder.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FacilityUnit).WithMany().HasForeignKey(x => x.FacilityUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class InmateMovementEventConfiguration : IEntityTypeConfiguration<InmateMovementEvent>
{
    public void Configure(EntityTypeBuilder<InmateMovementEvent> builder)
    {
        builder.ToTable("InmateMovementEvents", t =>
        {
            t.HasCheckConstraint("CK_InmateMovementEvents_NoSelfTransfer", "([FromFacilityId] IS NULL OR [ToFacilityId] IS NULL OR [FromFacilityId] <> [ToFacilityId] OR ISNULL([FromFacilityUnitId], '00000000-0000-0000-0000-000000000000') <> ISNULL([ToFacilityUnitId], '00000000-0000-0000-0000-000000000000'))");
            t.HasCheckConstraint("CK_InmateMovementEvents_Admission_Target", "([MovementType] <> 0) OR ([ToFacilityId] IS NOT NULL)");
            t.HasCheckConstraint("CK_InmateMovementEvents_Release_Source", "([MovementType] <> 1) OR ([FromFacilityId] IS NOT NULL)");
            t.HasCheckConstraint("CK_InmateMovementEvents_InternalTransfer_Units", "([MovementType] <> 4) OR ([FromFacilityUnitId] IS NOT NULL AND [ToFacilityUnitId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InmateReferenceHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SourceReference).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ExternalEventId).HasMaxLength(160);
        builder.Property(x => x.ReasonCode).HasMaxLength(80);
        builder.HasIndex(x => new { x.FacilityId, x.OccurredAtUtc, x.MovementType });
        builder.HasIndex(x => new { x.FromFacilityUnitId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.ToFacilityUnitId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.SourceType, x.SourceReference, x.ExternalEventId })
            .IsUnique()
            .HasFilter("[ExternalEventId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FromFacility).WithMany().HasForeignKey(x => x.FromFacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToFacility).WithMany().HasForeignKey(x => x.ToFacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FromFacilityUnit).WithMany().HasForeignKey(x => x.FromFacilityUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToFacilityUnit).WithMany().HasForeignKey(x => x.ToFacilityUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReversedByEvent).WithMany().HasForeignKey(x => x.ReversedByEventId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}
