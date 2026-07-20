namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.CorrectiveActions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class CorrectiveActionConfiguration : IEntityTypeConfiguration<CorrectiveAction>
{
    public void Configure(EntityTypeBuilder<CorrectiveAction> builder)
    {
        builder.ToTable("CorrectiveActions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.CompletionSummary).HasMaxLength(2000);
        builder.Property(x => x.ReopenReason).HasMaxLength(2000);
        builder.Property(x => x.CancelReason).HasMaxLength(2000);

        builder.HasIndex(x => x.ReferenceNumber).IsUnique();
        builder.HasIndex(x => x.OperationalNoteId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Priority);
        builder.HasIndex(x => x.DueAtUtc);
        builder.HasIndex(x => x.OwnerDepartmentId);
        builder.HasIndex(x => x.CreatedByUserId);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.Status, x.DueAtUtc });

        builder.HasOne(x => x.OperationalNote).WithMany(n => n.CorrectiveActions).HasForeignKey(x => x.OperationalNoteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OwnerDepartment).WithMany().HasForeignKey(x => x.OwnerDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CompletedByUser).WithMany().HasForeignKey(x => x.CompletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReopenedByUser).WithMany().HasForeignKey(x => x.ReopenedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CancelledByUser).WithMany().HasForeignKey(x => x.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LastProcessedByUser).WithMany().HasForeignKey(x => x.LastProcessedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureRowVersion();
    }
}

internal sealed class CorrectiveActionAssignmentConfiguration : IEntityTypeConfiguration<CorrectiveActionAssignment>
{
    public void Configure(EntityTypeBuilder<CorrectiveActionAssignment> builder)
    {
        builder.ToTable("CorrectiveActionAssignments", t =>
        {
            t.HasCheckConstraint(
                "CK_CorrectiveActionAssignments_UserXorDepartment",
                "([AssignedToUserId] IS NOT NULL AND [AssignedToDepartmentId] IS NULL) OR ([AssignedToUserId] IS NULL AND [AssignedToDepartmentId] IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.EndReason).HasMaxLength(2000);

        builder.HasIndex(x => x.CorrectiveActionId)
            .IsUnique()
            .HasFilter("[IsCurrent] = 1");

        builder.HasOne(x => x.CorrectiveAction).WithMany(a => a.Assignments).HasForeignKey(x => x.CorrectiveActionId)
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

internal sealed class CorrectiveActionStatusHistoryConfiguration : IEntityTypeConfiguration<CorrectiveActionStatusHistory>
{
    public void Configure(EntityTypeBuilder<CorrectiveActionStatusHistory> builder)
    {
        builder.ToTable("CorrectiveActionStatusHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.MetadataJson).HasMaxLength(4000);
        builder.HasIndex(x => new { x.CorrectiveActionId, x.ChangedAtUtc });

        builder.HasOne(x => x.CorrectiveAction).WithMany(a => a.StatusHistory).HasForeignKey(x => x.CorrectiveActionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Assignment).WithMany().HasForeignKey(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
