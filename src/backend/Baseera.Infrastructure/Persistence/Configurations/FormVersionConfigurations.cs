namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class FormVersionConfiguration : IEntityTypeConfiguration<FormVersion>
{
    public void Configure(EntityTypeBuilder<FormVersion> builder)
    {
        builder.ToTable("FormVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DraftSchemaJson).IsRequired();
        builder.Property(x => x.DraftSchemaHash).HasMaxLength(64);
        builder.HasIndex(x => new { x.FormDefinitionId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.SnapshotId);
        builder.HasOne(x => x.FormDefinition).WithMany(f => f.Versions).HasForeignKey(x => x.FormDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BasedOnVersion).WithMany().HasForeignKey(x => x.BasedOnVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Snapshot)
            .WithOne(s => s.FormVersion)
            .HasForeignKey<FormSchemaSnapshot>(s => s.FormVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class FormSchemaSnapshotConfiguration : IEntityTypeConfiguration<FormSchemaSnapshot>
{
    public void Configure(EntityTypeBuilder<FormSchemaSnapshot> builder)
    {
        builder.ToTable("FormSchemaSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CanonicalSchemaJson).IsRequired();
        builder.Property(x => x.SchemaHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.FormVersionId).IsUnique();
        builder.HasIndex(x => x.SchemaHash);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        // FormVersion relationship configured on FormVersion side to avoid duplicate FK.
    }
}

internal sealed class FormVersionReviewDecisionConfiguration : IEntityTypeConfiguration<FormVersionReviewDecision>
{
    public void Configure(EntityTypeBuilder<FormVersionReviewDecision> builder)
    {
        builder.ToTable("FormVersionReviewDecisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.HasIndex(x => x.FormVersionId);
        builder.HasIndex(x => x.ReviewedAtUtc);
        builder.HasOne(x => x.FormVersion).WithMany(v => v.ReviewDecisions).HasForeignKey(x => x.FormVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FormDefinitionVersionCounterConfiguration : IEntityTypeConfiguration<FormDefinitionVersionCounter>
{
    public void Configure(EntityTypeBuilder<FormDefinitionVersionCounter> builder)
    {
        builder.ToTable("FormDefinitionVersionCounters");
        builder.HasKey(x => x.FormDefinitionId);
        builder.Property(x => x.NextVersionNumber).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.FormDefinition).WithOne().HasForeignKey<FormDefinitionVersionCounter>(x => x.FormDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FormTemplateConfiguration : IEntityTypeConfiguration<FormTemplate>
{
    public void Configure(EntityTypeBuilder<FormTemplate> builder)
    {
        builder.ToTable("FormTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(120).IsRequired();
        builder.Property(x => x.CanonicalSchemaJson).IsRequired();
        builder.Property(x => x.SchemaHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.Category);
        builder.HasOne(x => x.OwnerDepartment).WithMany().HasForeignKey(x => x.OwnerDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SourceFormDefinition).WithMany().HasForeignKey(x => x.SourceFormDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SourceFormVersion).WithMany().HasForeignKey(x => x.SourceFormVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}
