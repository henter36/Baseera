namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class FormCampaignConfiguration : IEntityTypeConfiguration<FormCampaign>
{
    public void Configure(EntityTypeBuilder<FormCampaign> builder)
    {
        builder.ToTable("FormCampaigns", t =>
        {
            t.HasCheckConstraint("CK_FormCampaigns_ResponseWindowMinutes", "[ResponseWindowMinutes] > 0");
            t.HasCheckConstraint("CK_FormCampaigns_GracePeriodMinutes", "[GracePeriodMinutes] >= 0");
            t.HasCheckConstraint("CK_FormCampaigns_CloseAfterMinutes", "[CloseAfterMinutes] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.SchemaHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RecurrenceConfigurationJson).IsRequired();
        builder.Property(x => x.PauseReason).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.Status, x.NextOccurrenceUtc });
        builder.HasIndex(x => x.FormDefinitionId);
        builder.HasIndex(x => x.FormVersionId);
        builder.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FormDefinition).WithMany().HasForeignKey(x => x.FormDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FormVersion).WithMany().HasForeignKey(x => x.FormVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FormSchemaSnapshot).WithMany().HasForeignKey(x => x.FormSchemaSnapshotId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PublishedByUser).WithMany().HasForeignKey(x => x.PublishedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PausedByUser).WithMany().HasForeignKey(x => x.PausedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CancelledByUser).WithMany().HasForeignKey(x => x.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ClosedByUser).WithMany().HasForeignKey(x => x.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeletedByUser).WithMany().HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormTargetRuleConfiguration : IEntityTypeConfiguration<FormTargetRule>
{
    public void Configure(EntityTypeBuilder<FormTargetRule> builder)
    {
        builder.ToTable("FormTargetRules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ConfigurationJson).IsRequired();
        builder.HasIndex(x => x.CampaignId);
        builder.HasOne(x => x.Campaign).WithMany(c => c.TargetRules).HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormCampaignExclusionConfiguration : IEntityTypeConfiguration<FormCampaignExclusion>
{
    public void Configure(EntityTypeBuilder<FormCampaignExclusion> builder)
    {
        builder.ToTable("FormCampaignExclusions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => new { x.CampaignId, x.FacilityId }).IsUnique();
        builder.HasOne(x => x.Campaign).WithMany(c => c.Exclusions).HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormCycleConfiguration : IEntityTypeConfiguration<FormCycle>
{
    public void Configure(EntityTypeBuilder<FormCycle> builder)
    {
        builder.ToTable("FormCycles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OccurrenceKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SchemaHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TargetSnapshotHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.GeneratedBy).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.CampaignId, x.OccurrenceKey }).IsUnique();
        builder.HasIndex(x => new { x.CampaignId, x.SequenceNumber }).IsUnique();
        builder.HasAlternateKey(x => new { x.CampaignId, x.Id });
        builder.HasIndex(x => new { x.Status, x.OpenAtUtc });
        builder.HasIndex(x => x.DueAtUtc);
        builder.HasIndex(x => x.CloseAtUtc);
        builder.HasOne(x => x.Campaign).WithMany(c => c.Cycles).HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FormVersion).WithMany().HasForeignKey(x => x.FormVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FormSchemaSnapshot).WithMany().HasForeignKey(x => x.FormSchemaSnapshotId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormFacilityAssignmentConfiguration : IEntityTypeConfiguration<FormFacilityAssignment>
{
    public void Configure(EntityTypeBuilder<FormFacilityAssignment> builder)
    {
        builder.ToTable("FormFacilityAssignments");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CampaignId, x.CycleId, x.FacilityId });
        builder.Property(x => x.FacilityCodeAtAssignment).HasMaxLength(80).IsRequired();
        builder.Property(x => x.FacilityNameArAtAssignment).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RegionNameArAtAssignment).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FacilityTypeAtAssignment).HasMaxLength(100);
        builder.Property(x => x.UnavailableReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.CycleId, x.FacilityId }).IsUnique();
        builder.HasIndex(x => new { x.CampaignId, x.CycleId });
        builder.HasIndex(x => x.FacilityId);
        builder.HasIndex(x => x.RegionIdAtAssignment);
        builder.HasIndex(x => x.CampaignId);
        // Campaign FK retained for direct lookups; Restrict avoids cascade/ambiguity with cycle composite FK.
        builder.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Cycle)
            .WithMany(c => c.Assignments)
            .HasForeignKey(x => new { x.CampaignId, x.CycleId })
            .HasPrincipalKey(x => new { x.CampaignId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class OrganizationBusinessCalendarDateConfiguration : IEntityTypeConfiguration<OrganizationBusinessCalendarDate>
{
    public void Configure(EntityTypeBuilder<OrganizationBusinessCalendarDate> builder)
    {
        builder.ToTable("OrganizationBusinessCalendarDates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.OrganizationId, x.LocalDate }).IsUnique();
        builder.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}
