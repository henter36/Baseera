namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.SensitiveCustody;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class WeaponTypeDefinitionConfiguration : IEntityTypeConfiguration<WeaponTypeDefinition>
{
    public void Configure(EntityTypeBuilder<WeaponTypeDefinition> builder)
    {
        builder.ToTable("WeaponTypeDefinitions", table =>
        {
            table.HasCheckConstraint("CK_WeaponTypeDefinitions_InspectionInterval", "[InspectionIntervalDays] > 0");
            table.HasCheckConstraint("CK_WeaponTypeDefinitions_MaintenanceInterval", "[MaintenanceIntervalDays] IS NULL OR [MaintenanceIntervalDays] > 0");
        });
        builder.HasKey(type => type.Id);
        builder.Property(type => type.Code).HasMaxLength(80).IsRequired();
        builder.Property(type => type.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(type => type.Caliber).HasMaxLength(80).IsRequired();
        builder.HasIndex(type => new { type.OrganizationId, type.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(type => type.Organization).WithMany().HasForeignKey(type => type.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class ArmoryLocationConfiguration : IEntityTypeConfiguration<ArmoryLocation>
{
    public void Configure(EntityTypeBuilder<ArmoryLocation> builder)
    {
        builder.ToTable("ArmoryLocations", table =>
        {
            table.HasCheckConstraint("CK_ArmoryLocations_Capacity", "[Capacity] IS NULL OR [Capacity] >= 0");
        });
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Code).HasMaxLength(80).IsRequired();
        builder.Property(location => location.Name).HasMaxLength(200).IsRequired();
        builder.Property(location => location.LocationClassification).HasMaxLength(80).IsRequired();
        builder.HasIndex(location => new { location.FacilityId, location.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(location => location.Organization).WithMany().HasForeignKey(location => location.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(location => location.Facility).WithMany().HasForeignKey(location => location.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(location => location.FacilityUnit)
            .WithMany()
            .HasForeignKey(location => new { location.FacilityId, location.FacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(location => location.ResponsibleWorkforceMember).WithMany().HasForeignKey(location => location.ResponsibleWorkforceMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(location => location.AlternateResponsibleWorkforceMember).WithMany().HasForeignKey(location => location.AlternateResponsibleWorkforceMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class WeaponAssetConfiguration : IEntityTypeConfiguration<WeaponAsset>
{
    public void Configure(EntityTypeBuilder<WeaponAsset> builder)
    {
        builder.ToTable("WeaponAssets");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.InternalAssetCode).HasMaxLength(80).IsRequired();
        builder.Property(asset => asset.SerialNumberEncrypted).HasMaxLength(1024).IsRequired();
        builder.Property(asset => asset.SerialNumberHash).HasMaxLength(128).IsRequired();
        builder.Property(asset => asset.Manufacturer).HasMaxLength(120);
        builder.Property(asset => asset.Model).HasMaxLength(120);
        builder.Property(asset => asset.Caliber).HasMaxLength(80).IsRequired();
        builder.Property(asset => asset.AcquisitionReference).HasMaxLength(160);
        builder.Property(asset => asset.SourceReference).HasMaxLength(160);
        builder.HasIndex(asset => new { asset.OrganizationId, asset.InternalAssetCode }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(asset => new { asset.OrganizationId, asset.SerialNumberHash }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(asset => new { asset.CurrentFacilityId, asset.CurrentStatus, asset.NextInspectionDueAtUtc });
        builder.HasOne(asset => asset.Organization).WithMany().HasForeignKey(asset => asset.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(asset => asset.WeaponType).WithMany().HasForeignKey(asset => asset.WeaponTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(asset => asset.CurrentFacility).WithMany().HasForeignKey(asset => asset.CurrentFacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(asset => asset.CurrentFacilityUnit)
            .WithMany()
            .HasForeignKey(asset => new { asset.CurrentFacilityId, asset.CurrentFacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(asset => asset.CurrentArmoryLocation).WithMany().HasForeignKey(asset => asset.CurrentArmoryLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(asset => asset.CurrentCustodyTransaction)
            .WithOne()
            .HasForeignKey<WeaponAsset>(asset => asset.CurrentCustodyTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class CustodyTransactionConfiguration : IEntityTypeConfiguration<CustodyTransaction>
{
    public void Configure(EntityTypeBuilder<CustodyTransaction> builder)
    {
        builder.ToTable("CustodyTransactions", table =>
        {
            table.HasCheckConstraint("CK_CustodyTransactions_ReturnWindow", "[ExpectedReturnAtUtc] IS NULL OR [ExpectedReturnAtUtc] > [IssuedAtUtc]");
            table.HasCheckConstraint("CK_CustodyTransactions_ReturnedAfterIssue", "[ReturnedAtUtc] IS NULL OR [ReturnedAtUtc] >= [IssuedAtUtc]");
            table.HasCheckConstraint("CK_CustodyTransactions_IssueRequiresDestination", "([TransactionType] NOT IN (0, 1, 3, 4)) OR ([ToCustodyType] <> 5 AND [ToCustodyReferenceId] IS NOT NULL)");
            table.HasCheckConstraint("CK_CustodyTransactions_NoSelfApproval", "[ApprovedBy] IS NULL OR [ApprovedBy] <> [CreatedBy]");
        });
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.PurposeCode).HasMaxLength(80).IsRequired();
        builder.Property(transaction => transaction.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(transaction => transaction.CreatedBy).HasMaxLength(160).IsRequired();
        builder.Property(transaction => transaction.ApprovedBy).HasMaxLength(160);
        builder.Property(transaction => transaction.ReceivedBy).HasMaxLength(160);
        builder.Property(transaction => transaction.WitnessedBy).HasMaxLength(160);
        builder.Property(transaction => transaction.SourceReference).HasMaxLength(160);
        builder.HasIndex(transaction => transaction.WeaponAssetId).IsUnique().HasFilter("[IsDeleted] = 0 AND [IsCurrent] = 1");
        builder.HasIndex(transaction => new { transaction.FacilityId, transaction.Status, transaction.ExpectedReturnAtUtc });
        builder.HasOne(transaction => transaction.Organization).WithMany().HasForeignKey(transaction => transaction.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.Facility).WithMany().HasForeignKey(transaction => transaction.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.WeaponAsset).WithMany(asset => asset.CustodyTransactions).HasForeignKey(transaction => transaction.WeaponAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.PreviousTransaction).WithMany().HasForeignKey(transaction => transaction.PreviousTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.CorrectionOfTransaction).WithMany().HasForeignKey(transaction => transaction.CorrectionOfTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class AmmunitionTypeConfiguration : IEntityTypeConfiguration<AmmunitionType>
{
    public void Configure(EntityTypeBuilder<AmmunitionType> builder)
    {
        builder.ToTable("AmmunitionTypes");
        builder.HasKey(type => type.Id);
        builder.Property(type => type.Code).HasMaxLength(80).IsRequired();
        builder.Property(type => type.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(type => type.Caliber).HasMaxLength(80).IsRequired();
        builder.HasIndex(type => new { type.OrganizationId, type.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(type => type.Organization).WithMany().HasForeignKey(type => type.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class AmmunitionLotConfiguration : IEntityTypeConfiguration<AmmunitionLot>
{
    public void Configure(EntityTypeBuilder<AmmunitionLot> builder)
    {
        builder.ToTable("AmmunitionLots", table =>
        {
            table.HasCheckConstraint("CK_AmmunitionLots_Quantities", "[ReceivedQuantity] >= 0 AND [CurrentQuantity] >= 0 AND [ReservedQuantity] >= 0 AND [QuarantinedQuantity] >= 0 AND [DamagedQuantity] >= 0");
            table.HasCheckConstraint("CK_AmmunitionLots_Available", "[CurrentQuantity] >= [ReservedQuantity] + [QuarantinedQuantity] + [DamagedQuantity]");
        });
        builder.HasKey(lot => lot.Id);
        builder.Property(lot => lot.LotNumberEncrypted).HasMaxLength(512);
        builder.Property(lot => lot.LotNumberHash).HasMaxLength(128);
        builder.Property(lot => lot.UnitOfMeasure).HasMaxLength(40).IsRequired();
        builder.Property(lot => lot.SourceReference).HasMaxLength(160).IsRequired();
        builder.HasIndex(lot => new { lot.FacilityId, lot.AmmunitionTypeId, lot.ExpiryDateUtc });
        builder.HasIndex(lot => new { lot.OrganizationId, lot.LotNumberHash }).HasFilter("[IsDeleted] = 0 AND [LotNumberHash] IS NOT NULL");
        builder.HasOne(lot => lot.Organization).WithMany().HasForeignKey(lot => lot.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(lot => lot.Facility).WithMany().HasForeignKey(lot => lot.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(lot => lot.ArmoryLocation).WithMany().HasForeignKey(lot => lot.ArmoryLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(lot => lot.AmmunitionType).WithMany().HasForeignKey(lot => lot.AmmunitionTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class AmmunitionTransactionConfiguration : IEntityTypeConfiguration<AmmunitionTransaction>
{
    public void Configure(EntityTypeBuilder<AmmunitionTransaction> builder)
    {
        builder.ToTable("AmmunitionTransactions", table =>
        {
            table.HasCheckConstraint("CK_AmmunitionTransactions_Quantity", "[Quantity] > 0");
        });
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(transaction => transaction.CreatedBy).HasMaxLength(160).IsRequired();
        builder.Property(transaction => transaction.ApprovedBy).HasMaxLength(160);
        builder.Property(transaction => transaction.Reference).HasMaxLength(160);
        builder.HasIndex(transaction => new { transaction.FacilityId, transaction.OccurredAtUtc });
        builder.HasOne(transaction => transaction.Organization).WithMany().HasForeignKey(transaction => transaction.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.Facility).WithMany().HasForeignKey(transaction => transaction.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.AmmunitionLot).WithMany(lot => lot.Transactions).HasForeignKey(transaction => transaction.AmmunitionLotId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class SensitiveResourceRequirementConfiguration : IEntityTypeConfiguration<SensitiveResourceRequirement>
{
    public void Configure(EntityTypeBuilder<SensitiveResourceRequirement> builder)
    {
        builder.ToTable("SensitiveResourceRequirements", table =>
        {
            table.HasCheckConstraint("CK_SensitiveResourceRequirements_Quantities", "[RequiredQuantity] >= 0 AND [MinimumOperationalQuantity] >= 0 AND [MinimumOperationalQuantity] <= [RequiredQuantity]");
            table.HasCheckConstraint("CK_SensitiveResourceRequirements_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
            table.HasCheckConstraint("CK_SensitiveResourceRequirements_Target", "([WeaponTypeId] IS NOT NULL AND [AmmunitionTypeId] IS NULL) OR ([WeaponTypeId] IS NULL AND [AmmunitionTypeId] IS NOT NULL)");
        });
        builder.HasKey(requirement => requirement.Id);
        builder.Property(requirement => requirement.OperationalRole).HasMaxLength(120);
        builder.Property(requirement => requirement.ApprovalReference).HasMaxLength(160).IsRequired();
        builder.HasIndex(requirement => new { requirement.FacilityId, requirement.WeaponTypeId, requirement.AmmunitionTypeId, requirement.EffectiveFromUtc });
        builder.HasOne(requirement => requirement.Organization).WithMany().HasForeignKey(requirement => requirement.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.Facility).WithMany().HasForeignKey(requirement => requirement.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.FacilityUnit)
            .WithMany()
            .HasForeignKey(requirement => new { requirement.FacilityId, requirement.FacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.WeaponType).WithMany().HasForeignKey(requirement => requirement.WeaponTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.AmmunitionType).WithMany().HasForeignKey(requirement => requirement.AmmunitionTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class InventorySessionConfiguration : IEntityTypeConfiguration<InventorySession>
{
    public void Configure(EntityTypeBuilder<InventorySession> builder)
    {
        builder.ToTable("InventorySessions", table =>
        {
            table.HasCheckConstraint("CK_InventorySessions_Counts", "[ExpectedWeaponCount] >= 0 AND [CountedWeaponCount] >= 0 AND [ExpectedAmmunitionQuantity] >= 0 AND [CountedAmmunitionQuantity] >= 0");
            table.HasCheckConstraint("CK_InventorySessions_CompletedAfterStart", "[CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [StartedAtUtc]");
            table.HasCheckConstraint("CK_InventorySessions_NoSelfApproval", "[ApprovedBy] IS NULL OR [ApprovedBy] <> [InitiatedBy]");
        });
        builder.HasKey(session => session.Id);
        builder.Property(session => session.InitiatedBy).HasMaxLength(160).IsRequired();
        builder.Property(session => session.ApprovedBy).HasMaxLength(160);
        builder.Property(session => session.WitnessedBy).HasMaxLength(160);
        builder.Property(session => session.Notes).HasMaxLength(1000);
        builder.HasIndex(session => new { session.FacilityId, session.Status, session.StartedAtUtc });
        builder.HasOne(session => session.Organization).WithMany().HasForeignKey(session => session.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(session => session.Facility).WithMany().HasForeignKey(session => session.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(session => session.ArmoryLocation).WithMany().HasForeignKey(session => session.ArmoryLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class InventoryEntryConfiguration : IEntityTypeConfiguration<InventoryEntry>
{
    public void Configure(EntityTypeBuilder<InventoryEntry> builder)
    {
        builder.ToTable("InventoryEntries", table =>
        {
            table.HasCheckConstraint("CK_InventoryEntries_Quantities", "[ExpectedQuantity] IS NULL OR [ExpectedQuantity] >= 0");
            table.HasCheckConstraint("CK_InventoryEntries_CountedQuantity", "[CountedQuantity] IS NULL OR [CountedQuantity] >= 0");
        });
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Notes).HasMaxLength(1000);
        builder.Property(entry => entry.VerifiedBy).HasMaxLength(160).IsRequired();
        builder.HasIndex(entry => new { entry.InventorySessionId, entry.DiscrepancyType, entry.ResolvedAtUtc });
        builder.HasOne(entry => entry.InventorySession).WithMany(session => session.Entries).HasForeignKey(entry => entry.InventorySessionId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class WeaponInspectionConfiguration : IEntityTypeConfiguration<WeaponInspection>
{
    public void Configure(EntityTypeBuilder<WeaponInspection> builder)
    {
        builder.ToTable("WeaponInspections");
        builder.HasKey(inspection => inspection.Id);
        builder.Property(inspection => inspection.Restrictions).HasMaxLength(1000);
        builder.Property(inspection => inspection.StatusTransition).HasMaxLength(80).IsRequired();
        builder.Property(inspection => inspection.AttachmentReference).HasMaxLength(160);
        builder.HasIndex(inspection => new { inspection.FacilityId, inspection.NextDueAtUtc });
        builder.HasOne(inspection => inspection.Organization).WithMany().HasForeignKey(inspection => inspection.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(inspection => inspection.Facility).WithMany().HasForeignKey(inspection => inspection.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(inspection => inspection.WeaponAsset).WithMany(asset => asset.Inspections).HasForeignKey(inspection => inspection.WeaponAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(inspection => inspection.InspectorWorkforceMember).WithMany().HasForeignKey(inspection => inspection.InspectorWorkforceMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class SensitiveCustodyImportBatchConfiguration : IEntityTypeConfiguration<SensitiveCustodyImportBatch>
{
    public void Configure(EntityTypeBuilder<SensitiveCustodyImportBatch> builder)
    {
        builder.ToTable("SensitiveCustodyImportBatches", table =>
        {
            table.HasCheckConstraint("CK_SensitiveCustodyImportBatches_RowTotals", "[ValidRows] + [RejectedRows] + [DuplicateRows] = [TotalRows] AND [TotalRows] >= 0");
            table.HasCheckConstraint("CK_SensitiveCustodyImportBatches_AppliedRows", "[AppliedRows] >= 0 AND [AppliedRows] <= [ValidRows]");
        });
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.SourceSystem).HasMaxLength(80).IsRequired();
        builder.Property(batch => batch.SourceReference).HasMaxLength(160).IsRequired();
        builder.Property(batch => batch.FileHash).HasMaxLength(128).IsRequired();
        builder.Property(batch => batch.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(batch => new { batch.FacilityId, batch.ImportKind, batch.FileHash }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(batch => batch.Organization).WithMany().HasForeignKey(batch => batch.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(batch => batch.Facility).WithMany().HasForeignKey(batch => batch.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class SensitiveCustodyReconciliationResolutionConfiguration : IEntityTypeConfiguration<SensitiveCustodyReconciliationResolution>
{
    public void Configure(EntityTypeBuilder<SensitiveCustodyReconciliationResolution> builder)
    {
        builder.ToTable("SensitiveCustodyReconciliationResolutions");
        builder.HasKey(resolution => resolution.Id);
        builder.Property(resolution => resolution.ItemKey).HasMaxLength(160).IsRequired();
        builder.Property(resolution => resolution.Action).HasMaxLength(80).IsRequired();
        builder.Property(resolution => resolution.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(resolution => resolution.ResolvedBy).HasMaxLength(160).IsRequired();
        builder.HasIndex(resolution => new { resolution.FacilityId, resolution.ItemKey }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(resolution => resolution.Organization).WithMany().HasForeignKey(resolution => resolution.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(resolution => resolution.Facility).WithMany().HasForeignKey(resolution => resolution.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}
