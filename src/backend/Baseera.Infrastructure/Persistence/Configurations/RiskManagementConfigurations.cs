namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class RiskCategoryConfiguration : IEntityTypeConfiguration<RiskCategory>
{
    public void Configure(EntityTypeBuilder<RiskCategory> builder)
    {
        builder.ToTable("RiskCategories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(80).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.HasIndex(c => new { c.OrganizationId, c.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(c => c.Organization).WithMany().HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ParentCategory).WithMany(c => c.ChildCategories).HasForeignKey(c => c.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskRecordConfiguration : IEntityTypeConfiguration<RiskRecord>
{
    public void Configure(EntityTypeBuilder<RiskRecord> builder)
    {
        builder.ToTable("RiskRecords", table =>
        {
            table.HasCheckConstraint("CK_RiskRecords_AcceptedRequiresUntil", "([Status] <> 7) OR ([AcceptedUntilUtc] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_RiskRecords_ClosedRequiresClosure",
                "([Status] <> 9) OR ([ClosedAtUtc] IS NOT NULL AND [ClosedBy] IS NOT NULL AND [ClosureReason] IS NOT NULL)");
            table.HasCheckConstraint("CK_RiskRecords_ReopenedCount", "[ReopenedCount] >= 0");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RiskCode).HasMaxLength(40).IsRequired();
        builder.Property(r => r.Title).HasMaxLength(300).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(4000);
        builder.Property(r => r.SourceReference).HasMaxLength(160);
        builder.Property(r => r.ClosedBy).HasMaxLength(160);
        builder.Property(r => r.ClosureReason).HasMaxLength(2000);
        builder.Property(r => r.LastReopenReason).HasMaxLength(1000);
        builder.Property(r => r.RecurrenceKey).HasMaxLength(300).IsRequired();
        builder.Property(r => r.CurrentTrendReasonAr).HasMaxLength(500);
        builder.Property(r => r.CurrentScore).HasPrecision(9, 2);

        builder.HasIndex(r => new { r.OrganizationId, r.RiskCode }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(r => new { r.FacilityId, r.Status, r.CurrentRatingBandId });
        builder.HasIndex(r => new { r.FacilityId, r.NextReviewDueAtUtc });
        builder.HasIndex(r => r.RecurrenceKey);
        builder.HasIndex(r => new { r.FacilityId, r.OwnerWorkforceMemberId });

        builder.HasOne(r => r.Organization).WithMany().HasForeignKey(r => r.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.RiskCategory).WithMany(c => c.RiskRecords).HasForeignKey(r => r.RiskCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Facility).WithMany().HasForeignKey(r => r.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.FacilityUnit)
            .WithMany()
            .HasForeignKey(r => new { r.FacilityId, r.FacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Region).WithMany().HasForeignKey(r => r.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.HeadquartersOrganization).WithMany().HasForeignKey(r => r.HeadquartersOrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.OwnerWorkforceMember).WithMany().HasForeignKey(r => r.OwnerWorkforceMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.OwnerUser).WithMany().HasForeignKey(r => r.OwnerUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Assessments).WithOne(a => a.RiskRecord).HasForeignKey(a => a.RiskRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Controls).WithOne(c => c.RiskRecord).HasForeignKey(c => c.RiskRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.TreatmentPlans).WithOne(p => p.RiskRecord).HasForeignKey(p => p.RiskRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.SourceLinks).WithOne(l => l.RiskRecord).HasForeignKey(l => l.RiskRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Reviews).WithOne(v => v.RiskRecord).HasForeignKey(v => v.RiskRecordId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CurrentInherentAssessment).WithMany().HasForeignKey(r => r.CurrentInherentAssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.CurrentAssessment).WithMany().HasForeignKey(r => r.CurrentAssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.CurrentResidualAssessment).WithMany().HasForeignKey(r => r.CurrentResidualAssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.CurrentRatingBand).WithMany().HasForeignKey(r => r.CurrentRatingBandId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskStatusHistoryConfiguration : IEntityTypeConfiguration<RiskStatusHistory>
{
    public void Configure(EntityTypeBuilder<RiskStatusHistory> builder)
    {
        builder.ToTable("RiskStatusHistories");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.ChangedBy).HasMaxLength(160).IsRequired();
        builder.Property(h => h.Reason).HasMaxLength(1000);
        builder.HasIndex(h => new { h.RiskRecordId, h.ChangedAtUtc });
        builder.HasOne(h => h.RiskRecord).WithMany().HasForeignKey(h => h.RiskRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RiskAssessmentMatrixConfiguration : IEntityTypeConfiguration<RiskAssessmentMatrix>
{
    public void Configure(EntityTypeBuilder<RiskAssessmentMatrix> builder)
    {
        builder.ToTable("RiskAssessmentMatrices", table =>
        {
            table.HasCheckConstraint("CK_RiskAssessmentMatrices_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
            table.HasCheckConstraint("CK_RiskAssessmentMatrices_Version", "[Version] > 0");
            table.HasCheckConstraint("CK_RiskAssessmentMatrices_WeightedRequiresWeights", "([ScoreFormula] <> 1) OR ([ImpactWeightingJson] IS NOT NULL)");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Code).HasMaxLength(80).IsRequired();
        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.ApprovedBy).HasMaxLength(160);
        builder.Property(m => m.ImpactWeightingJson).HasMaxLength(4000);
        builder.HasIndex(m => new { m.OrganizationId, m.Code, m.Version }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(m => m.OrganizationId).IsUnique().HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0 AND [Status] = 2");
        builder.HasOne(m => m.Organization).WithMany().HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.PreviousVersionMatrix).WithMany().HasForeignKey(m => m.PreviousVersionMatrixId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class LikelihoodLevelConfiguration : IEntityTypeConfiguration<LikelihoodLevel>
{
    public void Configure(EntityTypeBuilder<LikelihoodLevel> builder)
    {
        builder.ToTable("RiskLikelihoodLevels", table =>
        {
            table.HasCheckConstraint("CK_RiskLikelihoodLevels_NumericValue", "[NumericValue] > 0");
        });
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Code).HasMaxLength(40).IsRequired();
        builder.Property(l => l.Name).HasMaxLength(120).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(1000);
        builder.Property(l => l.Criteria).HasMaxLength(1000);
        builder.HasIndex(l => new { l.MatrixId, l.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(l => l.Matrix).WithMany(m => m.LikelihoodLevels).HasForeignKey(l => l.MatrixId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class ImpactDimensionConfiguration : IEntityTypeConfiguration<ImpactDimension>
{
    public void Configure(EntityTypeBuilder<ImpactDimension> builder)
    {
        builder.ToTable("RiskImpactDimensions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Code).HasMaxLength(40).IsRequired();
        builder.Property(d => d.NameAr).HasMaxLength(120).IsRequired();
        builder.Property(d => d.NameEn).HasMaxLength(120);
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.HasIndex(d => new { d.OrganizationId, d.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(d => d.Organization).WithMany().HasForeignKey(d => d.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class ImpactLevelConfiguration : IEntityTypeConfiguration<ImpactLevel>
{
    public void Configure(EntityTypeBuilder<ImpactLevel> builder)
    {
        builder.ToTable("RiskImpactLevels", table =>
        {
            table.HasCheckConstraint("CK_RiskImpactLevels_NumericValue", "[NumericValue] > 0");
        });
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Code).HasMaxLength(40).IsRequired();
        builder.Property(l => l.Name).HasMaxLength(120).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(1000);
        builder.Property(l => l.Criteria).HasMaxLength(1000);
        builder.HasIndex(l => new { l.MatrixId, l.ImpactDimensionId, l.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(l => l.Matrix).WithMany(m => m.ImpactLevels).HasForeignKey(l => l.MatrixId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.ImpactDimension).WithMany().HasForeignKey(l => l.ImpactDimensionId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskRatingBandConfiguration : IEntityTypeConfiguration<RiskRatingBand>
{
    public void Configure(EntityTypeBuilder<RiskRatingBand> builder)
    {
        builder.ToTable("RiskRatingBands", table =>
        {
            table.HasCheckConstraint("CK_RiskRatingBands_ScoreRange", "[MinimumScore] <= [MaximumScore]");
        });
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Code).HasMaxLength(40).IsRequired();
        builder.Property(b => b.LabelAr).HasMaxLength(120).IsRequired();
        builder.Property(b => b.ColorToken).HasMaxLength(40).IsRequired();
        builder.Property(b => b.MinimumScore).HasPrecision(9, 2);
        builder.Property(b => b.MaximumScore).HasPrecision(9, 2);
        builder.HasIndex(b => new { b.MatrixId, b.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(b => b.Matrix).WithMany(m => m.RatingBands).HasForeignKey(b => b.MatrixId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskAssessmentConfiguration : IEntityTypeConfiguration<RiskAssessment>
{
    public void Configure(EntityTypeBuilder<RiskAssessment> builder)
    {
        builder.ToTable("RiskAssessments", table =>
        {
            table.HasCheckConstraint("CK_RiskAssessments_ScoreNonNegative", "[CalculatedScore] >= 0");
            table.HasCheckConstraint("CK_RiskAssessments_MatrixVersion", "[MatrixVersion] > 0");
        });
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Rationale).HasMaxLength(2000);
        builder.Property(a => a.AssessedBy).HasMaxLength(160).IsRequired();
        builder.Property(a => a.ReviewedBy).HasMaxLength(160);
        builder.Property(a => a.ApprovedBy).HasMaxLength(160);
        builder.Property(a => a.RejectionReason).HasMaxLength(1000);
        builder.Property(a => a.ClosureChangeSummary).HasMaxLength(2000);
        builder.Property(a => a.CalculatedScore).HasPrecision(9, 2);
        builder.HasIndex(a => new { a.RiskRecordId, a.AssessmentType, a.Status, a.ApprovedAtUtc });
        builder.HasOne(a => a.Matrix).WithMany().HasForeignKey(a => a.MatrixId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.LikelihoodLevel).WithMany().HasForeignKey(a => a.LikelihoodLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.OverallImpactLevel).WithMany().HasForeignKey(a => a.OverallImpactLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.RatingBand).WithMany().HasForeignKey(a => a.RatingBandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.SupersedesAssessment).WithMany().HasForeignKey(a => a.SupersedesAssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskAssessmentImpactConfiguration : IEntityTypeConfiguration<RiskAssessmentImpact>
{
    public void Configure(EntityTypeBuilder<RiskAssessmentImpact> builder)
    {
        builder.ToTable("RiskAssessmentImpacts");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.RationaleAr).HasMaxLength(2000);
        builder.Property(i => i.EvidenceReference).HasMaxLength(160);
        builder.HasIndex(i => new { i.RiskAssessmentId, i.ImpactDimensionId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(i => i.RiskAssessment).WithMany(a => a.ImpactBreakdown).HasForeignKey(i => i.RiskAssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.ImpactDimension).WithMany().HasForeignKey(i => i.ImpactDimensionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.ImpactLevel).WithMany().HasForeignKey(i => i.ImpactLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskControlConfiguration : IEntityTypeConfiguration<RiskControl>
{
    public void Configure(EntityTypeBuilder<RiskControl> builder)
    {
        builder.ToTable("RiskControls");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.SourceReference).HasMaxLength(160);
        builder.HasIndex(c => new { c.RiskRecordId, c.ControlStatus, c.NextTestDueAtUtc });
        builder.HasOne(c => c.OwnerWorkforceMember).WithMany().HasForeignKey(c => c.OwnerWorkforceMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskTreatmentPlanConfiguration : IEntityTypeConfiguration<RiskTreatmentPlan>
{
    public void Configure(EntityTypeBuilder<RiskTreatmentPlan> builder)
    {
        builder.ToTable("RiskTreatmentPlans", table =>
        {
            table.HasCheckConstraint("CK_RiskTreatmentPlans_ApprovedRequiresApprover", "([ApprovalStatus] <> 2) OR ([ApprovedBy] IS NOT NULL)");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Objective).HasMaxLength(2000).IsRequired();
        builder.Property(p => p.ApprovedBy).HasMaxLength(160);
        builder.Property(p => p.CancellationReason).HasMaxLength(1000);
        builder.Property(p => p.TargetScore).HasPrecision(9, 2);
        builder.HasIndex(p => new { p.RiskRecordId, p.Status, p.DueAtUtc });
        builder.HasOne(p => p.OwnerWorkforceMember).WithMany().HasForeignKey(p => p.OwnerWorkforceMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.TargetLikelihoodLevel).WithMany().HasForeignKey(p => p.TargetLikelihoodLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.TargetImpactLevel).WithMany().HasForeignKey(p => p.TargetImpactLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Actions).WithOne(a => a.TreatmentPlan).HasForeignKey(a => a.TreatmentPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskTreatmentActionConfiguration : IEntityTypeConfiguration<RiskTreatmentAction>
{
    public void Configure(EntityTypeBuilder<RiskTreatmentAction> builder)
    {
        builder.ToTable("RiskTreatmentActions", table =>
        {
            table.HasCheckConstraint("CK_RiskTreatmentActions_NoSelfDependency", "[DependencyActionId] IS NULL OR [DependencyActionId] <> [Id]");
        });
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Title).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(2000);
        builder.Property(a => a.CompletionSummary).HasMaxLength(2000);
        builder.Property(a => a.BlockedReason).HasMaxLength(1000);
        builder.Property(a => a.CancellationReason).HasMaxLength(1000);
        builder.Property(a => a.VerifiedBy).HasMaxLength(160);
        builder.HasIndex(a => new { a.TreatmentPlanId, a.Status, a.DueAtUtc });
        builder.HasOne(a => a.AssignedToWorkforceMember).WithMany().HasForeignKey(a => a.AssignedToWorkforceMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.AssignedToUser).WithMany().HasForeignKey(a => a.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.DependencyAction).WithMany().HasForeignKey(a => a.DependencyActionId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskSourceLinkConfiguration : IEntityTypeConfiguration<RiskSourceLink>
{
    public void Configure(EntityTypeBuilder<RiskSourceLink> builder)
    {
        builder.ToTable("RiskSourceLinks");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.AddedBy).HasMaxLength(160).IsRequired();
        builder.Property(l => l.Rationale).HasMaxLength(2000);
        builder.Property(l => l.RemovalReason).HasMaxLength(1000);
        builder.HasIndex(l => new { l.RiskRecordId, l.SourceEntityType, l.SourceEntityId, l.RelationshipType }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(l => new { l.SourceEntityType, l.SourceEntityId });
        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskReviewConfiguration : IEntityTypeConfiguration<RiskReview>
{
    public void Configure(EntityTypeBuilder<RiskReview> builder)
    {
        builder.ToTable("RiskReviews", table =>
        {
            table.HasCheckConstraint("CK_RiskReviews_CompletedRequiresDecision", "([Status] <> 2) OR ([Decision] IS NOT NULL)");
        });
        builder.HasKey(v => v.Id);
        builder.Property(v => v.SubjectReferenceType).HasMaxLength(60).IsRequired();
        builder.Property(v => v.RequestedBy).HasMaxLength(160).IsRequired();
        builder.Property(v => v.Comments).HasMaxLength(2000);
        builder.HasIndex(v => new { v.RiskRecordId, v.ReviewType, v.Status });
        builder.HasOne(v => v.AssignedReviewer).WithMany().HasForeignKey(v => v.AssignedReviewerId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskImportBatchConfiguration : IEntityTypeConfiguration<RiskImportBatch>
{
    public void Configure(EntityTypeBuilder<RiskImportBatch> builder)
    {
        builder.ToTable("RiskImportBatches", table =>
        {
            table.HasCheckConstraint("CK_RiskImportBatches_RowTotals", "[ValidRows] + [RejectedRows] + [DuplicateRows] = [TotalRows] AND [TotalRows] >= 0");
            table.HasCheckConstraint("CK_RiskImportBatches_AppliedRows", "[AppliedRows] >= 0 AND [AppliedRows] <= [ValidRows]");
        });
        builder.HasKey(b => b.Id);
        builder.Property(b => b.SourceSystem).HasMaxLength(80).IsRequired();
        builder.Property(b => b.SourceReference).HasMaxLength(160).IsRequired();
        builder.Property(b => b.FileHash).HasMaxLength(128).IsRequired();
        builder.Property(b => b.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(b => new { b.FacilityId, b.ImportKind, b.FileHash }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(b => b.Organization).WithMany().HasForeignKey(b => b.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Facility).WithMany().HasForeignKey(b => b.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class RiskReconciliationRecordConfiguration : IEntityTypeConfiguration<RiskReconciliationRecord>
{
    public void Configure(EntityTypeBuilder<RiskReconciliationRecord> builder)
    {
        builder.ToTable("RiskReconciliationRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ItemKey).HasMaxLength(160).IsRequired();
        builder.Property(r => r.Action).HasMaxLength(80).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.ResolvedBy).HasMaxLength(160).IsRequired();
        builder.HasIndex(r => new { r.FacilityId, r.ItemKey }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(r => r.Organization).WithMany().HasForeignKey(r => r.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Facility).WithMany().HasForeignKey(r => r.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}
