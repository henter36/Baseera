namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class FormCampaignResponsePolicyConfiguration : IEntityTypeConfiguration<FormCampaignResponsePolicy>
{
    public void Configure(EntityTypeBuilder<FormCampaignResponsePolicy> builder)
    {
        builder.ToTable("FormCampaignResponsePolicies");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.CampaignId).IsUnique();
        builder.Property(x => x.RequiredApprovalLevels).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FormCampaignResponsePolicies_Levels",
            "([ReviewMode] = 0 AND [RequiredApprovalLevels] = 0) OR ([ReviewMode] = 1 AND [RequiredApprovalLevels] = 1) OR ([ReviewMode] = 2 AND [RequiredApprovalLevels] BETWEEN 2 AND 5)"));
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FormCampaignResponsePolicies_Completion",
            "NOT ([CompletionBasis] = 1 AND [ReviewMode] = 0)"));
        builder.HasOne(x => x.Campaign)
            .WithOne(c => c.ResponsePolicy)
            .HasForeignKey<FormCampaignResponsePolicy>(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormResponseConfiguration : IEntityTypeConfiguration<FormResponse>
{
    public void Configure(EntityTypeBuilder<FormResponse> builder)
    {
        builder.ToTable("FormResponses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SchemaHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DraftAnswersJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.DraftAnswersHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.AssignmentId).IsUnique();
        builder.HasIndex(x => new { x.FacilityId, x.Status });
        builder.HasIndex(x => new { x.CampaignId, x.Status });
        builder.HasIndex(x => new { x.CycleId, x.Status });
        builder.HasIndex(x => x.SubmittedAtUtc);
        builder.HasAlternateKey(x => new { x.Id, x.CampaignId, x.CycleId, x.FacilityId });
        builder.HasOne(x => x.Assignment)
            .WithMany()
            .HasForeignKey(x => new { x.AssignmentId, x.CampaignId, x.CycleId, x.FacilityId })
            .HasPrincipalKey(x => new { x.Id, x.CampaignId, x.CycleId, x.FacilityId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Campaign)
            .WithMany()
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Cycle)
            .WithMany()
            .HasForeignKey(x => new { x.CampaignId, x.CycleId })
            .HasPrincipalKey(x => new { x.CampaignId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility)
            .WithMany()
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FormSchemaSnapshot)
            .WithMany()
            .HasForeignKey(x => x.FormSchemaSnapshotId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LastSavedByUser).WithMany().HasForeignKey(x => x.LastSavedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubmittedByUser).WithMany().HasForeignKey(x => x.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormResponseSubmissionConfiguration : IEntityTypeConfiguration<FormResponseSubmission>
{
    public void Configure(EntityTypeBuilder<FormResponseSubmission> builder)
    {
        builder.ToTable("FormResponseSubmissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SchemaHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CanonicalAnswersJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.AnswersHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.AcknowledgementText).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ResponseId, x.SubmissionNumber }).IsUnique();
        builder.HasAlternateKey(x => new { x.Id, x.ResponseId });
        builder.HasOne(x => x.Response)
            .WithMany(r => r.Submissions)
            .HasForeignKey(x => x.ResponseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FormSchemaSnapshot)
            .WithMany()
            .HasForeignKey(x => x.FormSchemaSnapshotId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubmittedByUser)
            .WithMany()
            .HasForeignKey(x => x.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormResponseReviewDecisionConfiguration : IEntityTypeConfiguration<FormResponseReviewDecision>
{
    public void Configure(EntityTypeBuilder<FormResponseReviewDecision> builder)
    {
        builder.ToTable("FormResponseReviewDecisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ResponseId, x.ReviewLevel });
        builder.HasIndex(x => new { x.ResponseId, x.SubmissionId, x.ReviewLevel })
            .IsUnique()
            .HasDatabaseName("IX_FormResponseReviewDecisions_ApproveLevel")
            .HasFilter("[Decision] = 2");
        builder.HasOne(x => x.Response)
            .WithMany(r => r.ReviewDecisions)
            .HasForeignKey(x => x.ResponseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Submission)
            .WithMany(s => s.ReviewDecisions)
            .HasForeignKey(x => new { x.SubmissionId, x.ResponseId })
            .HasPrincipalKey(x => new { x.Id, x.ResponseId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormResponseReviewCommentConfiguration : IEntityTypeConfiguration<FormResponseReviewComment>
{
    public void Configure(EntityTypeBuilder<FormResponseReviewComment> builder)
    {
        builder.ToTable("FormResponseReviewComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FieldKey).HasMaxLength(120);
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.ResponseId, x.SubmissionId });
        builder.HasOne(x => x.Response)
            .WithMany(r => r.ReviewComments)
            .HasForeignKey(x => x.ResponseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Submission)
            .WithMany(s => s.ReviewComments)
            .HasForeignKey(x => new { x.SubmissionId, x.ResponseId })
            .HasPrincipalKey(x => new { x.Id, x.ResponseId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewDecision)
            .WithMany(d => d.Comments)
            .HasForeignKey(x => x.ReviewDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormResponseMutationConfiguration : IEntityTypeConfiguration<FormResponseMutation>
{
    public void Configure(EntityTypeBuilder<FormResponseMutation> builder)
    {
        builder.ToTable("FormResponseMutations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ResultPayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(x => new { x.ResponseId, x.ClientMutationId }).IsUnique();
        builder.HasOne(x => x.Response)
            .WithMany(r => r.Mutations)
            .HasForeignKey(x => x.ResponseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormResponseHistoryConfiguration : IEntityTypeConfiguration<FormResponseHistory>
{
    public void Configure(EntityTypeBuilder<FormResponseHistory> builder)
    {
        builder.ToTable("FormResponseHistories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.ResponseId, x.OccurredAtUtc });
        builder.HasOne(x => x.Response)
            .WithMany(r => r.History)
            .HasForeignKey(x => x.ResponseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
