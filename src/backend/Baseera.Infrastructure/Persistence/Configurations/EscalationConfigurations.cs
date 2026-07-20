namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.Escalations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class EscalationPolicyConfiguration : IEntityTypeConfiguration<EscalationPolicy>
{
    public void Configure(EntityTypeBuilder<EscalationPolicy> builder)
    {
        builder.Property(p => p.Code).HasMaxLength(100).IsRequired();
        builder.Property(p => p.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.RowVersion).IsRowVersion();
        builder.HasIndex(p => p.Code).IsUnique();
        builder.HasIndex(p => new { p.IsEnabled, p.TargetType, p.ScopeType });
        builder.HasIndex(p => p.IsDeleted);

        builder.HasOne(p => p.CreatedByUser).WithMany().HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ActivatedByUser).WithMany().HasForeignKey(p => p.ActivatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.DeactivatedByUser).WithMany().HasForeignKey(p => p.DeactivatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Region).WithMany().HasForeignKey(p => p.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Facility).WithMany().HasForeignKey(p => p.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.FacilityUnit).WithMany().HasForeignKey(p => p.FacilityUnitId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_EscalationPolicies_GlobalHq_NoIds",
                "([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_EscalationPolicies_Region_RequiresRegion",
                "([ScopeType] <> 2) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_EscalationPolicies_Facility_RequiresFacility",
                "([ScopeType] <> 3) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_EscalationPolicies_Unit_RequiresFacilityAndUnit",
                "([ScopeType] <> 4) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)");
        });
    }
}

public sealed class EscalationRuleConfiguration : IEntityTypeConfiguration<EscalationRule>
{
    public void Configure(EntityTypeBuilder<EscalationRule> builder)
    {
        builder.Property(r => r.RecipientRoleCode).HasMaxLength(100);
        builder.Property(r => r.TitleTemplateAr).HasMaxLength(300).IsRequired();
        builder.Property(r => r.MessageTemplateAr).HasMaxLength(1200).IsRequired();
        builder.Property(r => r.RowVersion).IsRowVersion();
        builder.HasIndex(r => new { r.EscalationPolicyId, r.Level }).IsUnique();
        builder.HasIndex(r => new { r.IsEnabled, r.TriggerType, r.ThresholdDays });
        builder.HasIndex(r => r.IsDeleted);
        builder.HasOne(r => r.EscalationPolicy).WithMany(p => p.Rules).HasForeignKey(r => r.EscalationPolicyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.SpecificRecipientUser).WithMany().HasForeignKey(r => r.SpecificRecipientUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_EscalationRules_Level_Positive", "[Level] > 0");
            t.HasCheckConstraint("CK_EscalationRules_Threshold_NonNegative", "[ThresholdDays] >= 0");
            t.HasCheckConstraint("CK_EscalationRules_Repeat_Positive_WhenPresent", "[RepeatEveryDays] IS NULL OR [RepeatEveryDays] > 0");
            t.HasCheckConstraint("CK_EscalationRules_Max_Positive_WhenPresent", "[MaximumOccurrences] IS NULL OR [MaximumOccurrences] > 0");
            t.HasCheckConstraint("CK_EscalationRules_RoleStrategy_RoleRequired", "([RecipientStrategy] <> 2) OR ([RecipientRoleCode] IS NOT NULL)");
            t.HasCheckConstraint("CK_EscalationRules_UserStrategy_UserRequired", "([RecipientStrategy] <> 1) OR ([SpecificRecipientUserId] IS NOT NULL)");
        });
    }
}

public sealed class EscalationOccurrenceConfiguration : IEntityTypeConfiguration<EscalationOccurrence>
{
    public void Configure(EntityTypeBuilder<EscalationOccurrence> builder)
    {
        builder.Property(o => o.TargetReferenceNumber).HasMaxLength(50).IsRequired();
        builder.Property(o => o.OccurrenceKey).HasMaxLength(300).IsRequired();
        builder.Property(o => o.SuppressionReason).HasMaxLength(500);
        builder.Property(o => o.CorrelationId).HasMaxLength(100);
        builder.HasKey(o => o.Id);
        builder.HasIndex(o => o.OccurrenceKey).IsUnique();
        builder.HasIndex(o => new { o.TargetType, o.TargetId });
        builder.HasIndex(o => new { o.TargetType, o.DueAtUtc });
        builder.HasIndex(o => o.Status);
        builder.HasOne(o => o.Policy).WithMany().HasForeignKey(o => o.PolicyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Rule).WithMany().HasForeignKey(o => o.RuleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.TargetReferenceNumber).HasMaxLength(50).IsRequired();
        builder.Property(n => n.TitleAr).HasMaxLength(300).IsRequired();
        builder.Property(n => n.MessageAr).HasMaxLength(1200).IsRequired();
        builder.Property(n => n.DeduplicationKey).HasMaxLength(400).IsRequired();
        builder.Property(n => n.RowVersion).IsRowVersion();
        builder.HasIndex(n => n.DeduplicationKey).IsUnique();
        builder.HasIndex(n => new { n.RecipientUserId, n.Status, n.CreatedAtUtc });
        builder.HasIndex(n => new { n.TargetType, n.TargetId });
        builder.HasOne(n => n.RecipientUser).WithMany().HasForeignKey(n => n.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.EscalationOccurrence).WithMany(o => o.Notifications).HasForeignKey(n => n.EscalationOccurrenceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationDeliveryAttemptConfiguration : IEntityTypeConfiguration<NotificationDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryAttempt> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ErrorCode).HasMaxLength(100);
        builder.Property(a => a.ErrorMessageSafe).HasMaxLength(500);
        builder.Property(a => a.ProviderMessageId).HasMaxLength(200);
        builder.Property(a => a.CorrelationId).HasMaxLength(100);
        builder.HasIndex(a => new { a.NotificationId, a.Channel, a.AttemptNumber }).IsUnique();
        builder.HasIndex(a => new { a.Status, a.NextRetryAtUtc });
        builder.HasOne(a => a.Notification).WithMany().HasForeignKey(a => a.NotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BackgroundJobLeaseConfiguration : IEntityTypeConfiguration<BackgroundJobLease>
{
    public void Configure(EntityTypeBuilder<BackgroundJobLease> builder)
    {
        builder.HasKey(l => l.JobName);
        builder.Property(l => l.JobName).HasMaxLength(100).IsRequired();
        builder.Property(l => l.LeaseOwner).HasMaxLength(200).IsRequired();
        builder.Property(l => l.RowVersion).IsRowVersion();
        builder.HasIndex(l => l.LeaseExpiresAtUtc);
    }
}
