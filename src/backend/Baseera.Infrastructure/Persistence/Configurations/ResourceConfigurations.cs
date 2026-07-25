namespace Baseera.Infrastructure.Persistence.Configurations;

using Baseera.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ResourceAssetConfiguration : IEntityTypeConfiguration<ResourceAsset>
{
    public void Configure(EntityTypeBuilder<ResourceAsset> builder)
    {
        builder.ToTable("ResourceAssets", table =>
        {
            table.HasCheckConstraint("CK_ResourceAssets_ManufactureYear", "[ManufactureYear] IS NULL OR ([ManufactureYear] >= 1950 AND [ManufactureYear] <= 2100)");
            table.HasCheckConstraint("CK_ResourceAssets_UnitRequiresFacility", "[OperationalFacilityUnitId] IS NULL OR [OperationalFacilityId] IS NOT NULL");
        });
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.AssetCode).HasMaxLength(80).IsRequired();
        builder.Property(asset => asset.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(asset => asset.SerialNumber).HasMaxLength(120);
        builder.Property(asset => asset.Manufacturer).HasMaxLength(120);
        builder.Property(asset => asset.Model).HasMaxLength(120);
        builder.Property(asset => asset.SourceReference).HasMaxLength(160);
        builder.Property(asset => asset.LastVerifiedBy).HasMaxLength(120);
        builder.HasIndex(asset => new { asset.OrganizationId, asset.AssetCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(asset => new { asset.OperationalFacilityId, asset.ResourceType, asset.CurrentStatus });
        builder.HasIndex(asset => new { asset.OperationalFacilityUnitId, asset.CurrentStatus });
        builder.HasOne(asset => asset.Organization).WithMany().HasForeignKey(asset => asset.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(asset => asset.OwnershipOrganization).WithMany().HasForeignKey(asset => asset.OwnershipOrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(asset => asset.OperationalFacility).WithMany().HasForeignKey(asset => asset.OperationalFacilityId).OnDelete(DeleteBehavior.Restrict);
        // Composite FK ensures unit belongs to the same facility when both keys are set.
        // OperationalFacilityId is nullable; CK_ResourceAssets_UnitRequiresFacility + application EnsureUnitInFacilityAsync cover partial states.
        builder.HasOne(asset => asset.OperationalFacilityUnit)
            .WithMany()
            .HasForeignKey(asset => new { asset.OperationalFacilityId, asset.OperationalFacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(asset => asset.CustodianUser).WithMany().HasForeignKey(asset => asset.CustodianUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class VehicleProfileConfiguration : IEntityTypeConfiguration<VehicleProfile>
{
    public void Configure(EntityTypeBuilder<VehicleProfile> builder)
    {
        builder.ToTable("VehicleProfiles", table =>
        {
            table.HasCheckConstraint("CK_VehicleProfiles_Odometer_NonNegative", "[Odometer] IS NULL OR [Odometer] >= 0");
            table.HasCheckConstraint("CK_VehicleProfiles_PassengerCapacity_NonNegative", "[PassengerCapacity] IS NULL OR [PassengerCapacity] >= 0");
            table.HasCheckConstraint("CK_VehicleProfiles_PrisonerTransportCapacity_NonNegative", "[PrisonerTransportCapacity] IS NULL OR [PrisonerTransportCapacity] >= 0");
        });
        builder.HasKey(profile => profile.ResourceAssetId);
        builder.Property(profile => profile.PlateNumber).HasMaxLength(40).IsRequired();
        builder.Property(profile => profile.VehicleIdentificationNumber).HasMaxLength(80);
        builder.Property(profile => profile.TrackerExternalId).HasMaxLength(120);
        builder.Property(profile => profile.OperationalRole).HasMaxLength(120);
        builder.HasIndex(profile => profile.PlateNumber).HasFilter("[PlateNumber] <> ''");
        builder.HasOne(profile => profile.ResourceAsset).WithOne(asset => asset.VehicleProfile).HasForeignKey<VehicleProfile>(profile => profile.ResourceAssetId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CommunicationDeviceProfileConfiguration : IEntityTypeConfiguration<CommunicationDeviceProfile>
{
    public void Configure(EntityTypeBuilder<CommunicationDeviceProfile> builder)
    {
        builder.ToTable("CommunicationDeviceProfiles");
        builder.HasKey(profile => profile.ResourceAssetId);
        builder.Property(profile => profile.NetworkType).HasMaxLength(80);
        builder.Property(profile => profile.CallSign).HasMaxLength(80);
        builder.Property(profile => profile.SimOrLineReference).HasMaxLength(120);
        builder.Property(profile => profile.FrequencyGroup).HasMaxLength(80);
        builder.Property(profile => profile.BatteryCondition).HasMaxLength(80);
        builder.Property(profile => profile.CoverageStatus).HasMaxLength(80);
        builder.HasOne(profile => profile.ResourceAsset).WithOne(asset => asset.CommunicationDeviceProfile).HasForeignKey<CommunicationDeviceProfile>(profile => profile.ResourceAssetId).OnDelete(DeleteBehavior.Restrict);
        // AssignedUnitId cannot use a composite facility-unit FK: the profile has no FacilityId column.
        // Facility membership is enforced in application services when assigning units.
        builder.HasOne(profile => profile.AssignedUnit).WithMany().HasForeignKey(profile => profile.AssignedUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EquipmentProfileConfiguration : IEntityTypeConfiguration<EquipmentProfile>
{
    public void Configure(EntityTypeBuilder<EquipmentProfile> builder)
    {
        builder.ToTable("EquipmentProfiles");
        builder.HasKey(profile => profile.ResourceAssetId);
        builder.Property(profile => profile.Specification).HasMaxLength(500);
        builder.Property(profile => profile.QuantityUnit).HasMaxLength(40);
        builder.HasIndex(profile => new { profile.EquipmentCategory, profile.InspectionDueAtUtc });
        builder.HasIndex(profile => new { profile.EquipmentCategory, profile.CalibrationDueAtUtc });
        builder.HasOne(profile => profile.ResourceAsset).WithOne(asset => asset.EquipmentProfile).HasForeignKey<EquipmentProfile>(profile => profile.ResourceAssetId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FacilityAssetProfileConfiguration : IEntityTypeConfiguration<FacilityAssetProfile>
{
    public void Configure(EntityTypeBuilder<FacilityAssetProfile> builder)
    {
        builder.ToTable("FacilityAssetProfiles", table =>
        {
            table.HasCheckConstraint("CK_FacilityAssetProfiles_Capacity_NonNegative", "[CapacityValue] IS NULL OR [CapacityValue] >= 0");
        });
        builder.HasKey(profile => profile.ResourceAssetId);
        builder.Property(profile => profile.InstalledAtLocation).HasMaxLength(200);
        builder.Property(profile => profile.CapacityUnit).HasMaxLength(40);
        builder.HasOne(profile => profile.ResourceAsset).WithOne(asset => asset.FacilityAssetProfile).HasForeignKey<FacilityAssetProfile>(profile => profile.ResourceAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(profile => profile.Building).WithMany().HasForeignKey(profile => profile.BuildingId).OnDelete(DeleteBehavior.Restrict);
        // FacilityUnitId cannot use a composite facility-unit FK: the profile has no FacilityId column.
        // Facility membership is enforced in application services when assigning units.
        builder.HasOne(profile => profile.FacilityUnit).WithMany().HasForeignKey(profile => profile.FacilityUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ResourceStatusEventConfiguration : IEntityTypeConfiguration<ResourceStatusEvent>
{
    public void Configure(EntityTypeBuilder<ResourceStatusEvent> builder)
    {
        builder.ToTable("ResourceStatusEvents");
        builder.HasKey(statusEvent => statusEvent.Id);
        builder.Property(statusEvent => statusEvent.ReasonCode).HasMaxLength(80);
        builder.Property(statusEvent => statusEvent.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(statusEvent => statusEvent.SourceReference).HasMaxLength(160);
        builder.HasIndex(statusEvent => new { statusEvent.ResourceAssetId, statusEvent.OccurredAtUtc });
        builder.HasOne(statusEvent => statusEvent.ResourceAsset).WithMany(asset => asset.StatusEvents).HasForeignKey(statusEvent => statusEvent.ResourceAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(statusEvent => statusEvent.RecordedByUser).WithMany().HasForeignKey(statusEvent => statusEvent.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class ResourcePlacementConfiguration : IEntityTypeConfiguration<ResourcePlacement>
{
    public void Configure(EntityTypeBuilder<ResourcePlacement> builder)
    {
        builder.ToTable("ResourcePlacements", table =>
        {
            table.HasCheckConstraint("CK_ResourcePlacements_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
        });
        builder.HasKey(placement => placement.Id);
        builder.Property(placement => placement.SourceReference).HasMaxLength(160);
        builder.Property(placement => placement.Reason).HasMaxLength(1000);
        builder.HasIndex(placement => placement.ResourceAssetId)
            .IsUnique()
            .HasFilter("[EffectiveToUtc] IS NULL");
        builder.HasIndex(placement => new { placement.OperationalFacilityId, placement.OperationalFacilityUnitId, placement.EffectiveFromUtc });
        builder.HasOne(placement => placement.ResourceAsset).WithMany(asset => asset.Placements).HasForeignKey(placement => placement.ResourceAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(placement => placement.OwnershipOrganization).WithMany().HasForeignKey(placement => placement.OwnershipOrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(placement => placement.OperationalFacility).WithMany().HasForeignKey(placement => placement.OperationalFacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(placement => placement.OperationalFacilityUnit)
            .WithMany()
            .HasForeignKey(placement => new { placement.OperationalFacilityId, placement.OperationalFacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(placement => placement.AssignedToUser).WithMany().HasForeignKey(placement => placement.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class MaintenanceWorkOrderConfiguration : IEntityTypeConfiguration<MaintenanceWorkOrder>
{
    public void Configure(EntityTypeBuilder<MaintenanceWorkOrder> builder)
    {
        builder.ToTable("MaintenanceWorkOrders", table =>
        {
            table.HasCheckConstraint("CK_MaintenanceWorkOrders_Dates", "[CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [ReportedAtUtc]");
            table.HasCheckConstraint("CK_MaintenanceWorkOrders_Downtime_NonNegative", "[DowntimeMinutes] IS NULL OR [DowntimeMinutes] >= 0");
            table.HasCheckConstraint("CK_MaintenanceWorkOrders_AwaitingParts_Date", "[PartsRequired] = 0 OR [WaitingForPartsSinceUtc] IS NOT NULL");
        });
        builder.HasKey(order => order.Id);
        builder.Property(order => order.WorkOrderNumber).HasMaxLength(80).IsRequired();
        builder.Property(order => order.ProblemDescription).HasMaxLength(2000).IsRequired();
        builder.Property(order => order.VendorReference).HasMaxLength(160);
        builder.Property(order => order.CompletionSummary).HasMaxLength(2000);
        builder.HasIndex(order => new { order.OrganizationId, order.WorkOrderNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(order => new { order.ResourceAssetId, order.Status, order.ExpectedCompletionAtUtc });
        builder.HasOne(order => order.Organization).WithMany().HasForeignKey(order => order.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.ResourceAsset).WithMany(asset => asset.MaintenanceWorkOrders).HasForeignKey(order => order.ResourceAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.ReportedByUser).WithMany().HasForeignKey(order => order.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.AssignedToUser).WithMany().HasForeignKey(order => order.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class ResourceRequirementConfiguration : IEntityTypeConfiguration<ResourceRequirement>
{
    public void Configure(EntityTypeBuilder<ResourceRequirement> builder)
    {
        builder.ToTable("ResourceRequirements", table =>
        {
            table.HasCheckConstraint("CK_ResourceRequirements_Quantities", "[RequiredQuantity] >= 0 AND [MinimumOperationalQuantity] >= 0 AND [MinimumOperationalQuantity] <= [RequiredQuantity]");
            table.HasCheckConstraint("CK_ResourceRequirements_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
        });
        builder.HasKey(requirement => requirement.Id);
        builder.Property(requirement => requirement.ResourceCategory).HasMaxLength(120).IsRequired();
        builder.Property(requirement => requirement.SourceReference).HasMaxLength(160).IsRequired();
        builder.Property(requirement => requirement.ApprovalReference).HasMaxLength(160);
        builder.Property(requirement => requirement.Notes).HasMaxLength(1000);
        builder.HasIndex(requirement => new { requirement.FacilityId, requirement.FacilityUnitId, requirement.ResourceType, requirement.ResourceCategory, requirement.EffectiveFromUtc });
        builder.HasIndex(requirement => new { requirement.FacilityId, requirement.ResourceType, requirement.ResourceCategory })
            .IsUnique()
            .HasDatabaseName("IX_ResourceRequirements_FacilityOpen")
            .HasFilter("[IsDeleted] = 0 AND [EffectiveToUtc] IS NULL AND [FacilityUnitId] IS NULL");
        builder.HasIndex(requirement => new { requirement.FacilityId, requirement.FacilityUnitId, requirement.ResourceType, requirement.ResourceCategory })
            .IsUnique()
            .HasDatabaseName("IX_ResourceRequirements_UnitOpen")
            .HasFilter("[IsDeleted] = 0 AND [EffectiveToUtc] IS NULL AND [FacilityUnitId] IS NOT NULL");
        builder.HasOne(requirement => requirement.Organization).WithMany().HasForeignKey(requirement => requirement.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.Facility).WithMany().HasForeignKey(requirement => requirement.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.FacilityUnit)
            .WithMany()
            .HasForeignKey(requirement => new { requirement.FacilityId, requirement.FacilityUnitId })
            .HasPrincipalKey(unit => new { unit.FacilityId, unit.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}

internal sealed class ResourceImportBatchConfiguration : IEntityTypeConfiguration<ResourceImportBatch>
{
    public void Configure(EntityTypeBuilder<ResourceImportBatch> builder)
    {
        builder.ToTable("ResourceImportBatches", table =>
        {
            table.HasCheckConstraint(
                "CK_ResourceImportBatches_RowTotals",
                "[ValidRows] + [RejectedRows] + [DuplicateRows] = [TotalRows] AND [TotalRows] >= 0 AND [ValidRows] >= 0 AND [RejectedRows] >= 0 AND [DuplicateRows] >= 0");
            table.HasCheckConstraint(
                "CK_ResourceImportBatches_AppliedRows",
                "[AppliedRows] >= 0 AND [AppliedRows] <= [ValidRows]");
            table.HasCheckConstraint(
                "CK_ResourceImportBatches_ConfirmedState",
                "([Status] = N'Confirmed' AND [ConfirmedAtUtc] IS NOT NULL) OR ([Status] <> N'Confirmed' AND ([Status] <> N'Previewed' OR ([AppliedRows] = 0 AND [ConfirmedAtUtc] IS NULL)))");
        });
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.SourceSystem).HasMaxLength(120).IsRequired();
        builder.Property(batch => batch.SourceReference).HasMaxLength(160).IsRequired();
        builder.Property(batch => batch.FileHash).HasMaxLength(128).IsRequired();
        builder.Property(batch => batch.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(batch => new { batch.FacilityId, batch.SourceSystem, batch.SourceReference, batch.FileHash }).IsUnique();
        builder.HasOne(batch => batch.Facility).WithMany().HasForeignKey(batch => batch.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(batch => batch.SubmittedByUser).WithMany().HasForeignKey(batch => batch.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureRowVersion();
    }
}
