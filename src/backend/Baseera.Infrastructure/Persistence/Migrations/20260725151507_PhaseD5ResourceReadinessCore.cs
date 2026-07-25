using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseD5ResourceReadinessCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "MaintenanceWorkOrderNumberSequence");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_FacilityUnits_FacilityId_Id",
                table: "FacilityUnits",
                columns: new[] { "FacilityId", "Id" });

            migrationBuilder.CreateTable(
                name: "ResourceAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceType = table.Column<int>(type: "int", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ManufactureYear = table.Column<int>(type: "int", nullable: true),
                    AcquisitionDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CommissionedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpectedEndOfLifeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OwnershipOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalFacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationalFacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustodianUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentStatus = table.Column<int>(type: "int", nullable: false),
                    Condition = table.Column<int>(type: "int", nullable: false),
                    Criticality = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    LastVerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastVerifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceAssets", x => x.Id);
                    table.CheckConstraint("CK_ResourceAssets_ManufactureYear", "[ManufactureYear] IS NULL OR ([ManufactureYear] >= 1950 AND [ManufactureYear] <= 2100)");
                    table.CheckConstraint("CK_ResourceAssets_UnitRequiresFacility", "[OperationalFacilityUnitId] IS NULL OR [OperationalFacilityId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_ResourceAssets_Facilities_OperationalFacilityId",
                        column: x => x.OperationalFacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceAssets_FacilityUnits_OperationalFacilityId_OperationalFacilityUnitId",
                        columns: x => new { x.OperationalFacilityId, x.OperationalFacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceAssets_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceAssets_Organizations_OwnershipOrganizationId",
                        column: x => x.OwnershipOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceAssets_Users_CustodianUserId",
                        column: x => x.CustodianUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ValidRows = table.Column<int>(type: "int", nullable: false),
                    RejectedRows = table.Column<int>(type: "int", nullable: false),
                    DuplicateRows = table.Column<int>(type: "int", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AppliedRows = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceImportBatches", x => x.Id);
                    table.CheckConstraint("CK_ResourceImportBatches_AppliedRows", "[AppliedRows] >= 0 AND [AppliedRows] <= [ValidRows]");
                    table.CheckConstraint("CK_ResourceImportBatches_ConfirmedState", "([Status] = N'Confirmed' AND [ConfirmedAtUtc] IS NOT NULL) OR ([Status] = N'Previewed' AND [AppliedRows] = 0 AND [ConfirmedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_ResourceImportBatches_RowTotals", "[ValidRows] + [RejectedRows] + [DuplicateRows] = [TotalRows] AND [TotalRows] >= 0 AND [ValidRows] >= 0 AND [RejectedRows] >= 0 AND [DuplicateRows] >= 0");
                    table.ForeignKey(
                        name: "FK_ResourceImportBatches_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceImportBatches_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResourceType = table.Column<int>(type: "int", nullable: false),
                    ResourceCategory = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RequiredQuantity = table.Column<int>(type: "int", nullable: false),
                    MinimumOperationalQuantity = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ApprovalReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceRequirements", x => x.Id);
                    table.CheckConstraint("CK_ResourceRequirements_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_ResourceRequirements_Quantities", "[RequiredQuantity] >= 0 AND [MinimumOperationalQuantity] >= 0 AND [MinimumOperationalQuantity] <= [RequiredQuantity]");
                    table.ForeignKey(
                        name: "FK_ResourceRequirements_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceRequirements_FacilityUnits_FacilityId_FacilityUnitId",
                        columns: x => new { x.FacilityId, x.FacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceRequirements_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationDeviceProfiles",
                columns: table => new
                {
                    ResourceAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceCategory = table.Column<int>(type: "int", nullable: false),
                    NetworkType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CallSign = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SimOrLineReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FrequencyGroup = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    BatteryCondition = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CoverageStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EncryptionCapability = table.Column<bool>(type: "bit", nullable: true),
                    AssignedUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationDeviceProfiles", x => x.ResourceAssetId);
                    table.ForeignKey(
                        name: "FK_CommunicationDeviceProfiles_FacilityUnits_AssignedUnitId",
                        column: x => x.AssignedUnitId,
                        principalTable: "FacilityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationDeviceProfiles_ResourceAssets_ResourceAssetId",
                        column: x => x.ResourceAssetId,
                        principalTable: "ResourceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentProfiles",
                columns: table => new
                {
                    ResourceAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentCategory = table.Column<int>(type: "int", nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuantityUnit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CalibrationRequired = table.Column<bool>(type: "bit", nullable: false),
                    CalibrationDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InspectionRequired = table.Column<bool>(type: "bit", nullable: false),
                    InspectionDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Portable = table.Column<bool>(type: "bit", nullable: false),
                    SafetyCritical = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentProfiles", x => x.ResourceAssetId);
                    table.ForeignKey(
                        name: "FK_EquipmentProfiles_ResourceAssets_ResourceAssetId",
                        column: x => x.ResourceAssetId,
                        principalTable: "ResourceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FacilityAssetProfiles",
                columns: table => new
                {
                    ResourceAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCategory = table.Column<int>(type: "int", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InstalledAtLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FixedAsset = table.Column<bool>(type: "bit", nullable: false),
                    CapacityValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CapacityUnit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RequiresPeriodicInspection = table.Column<bool>(type: "bit", nullable: false),
                    InspectionDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityAssetProfiles", x => x.ResourceAssetId);
                    table.CheckConstraint("CK_FacilityAssetProfiles_Capacity_NonNegative", "[CapacityValue] IS NULL OR [CapacityValue] >= 0");
                    table.ForeignKey(
                        name: "FK_FacilityAssetProfiles_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacilityAssetProfiles_FacilityUnits_FacilityUnitId",
                        column: x => x.FacilityUnitId,
                        principalTable: "FacilityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacilityAssetProfiles_ResourceAssets_ResourceAssetId",
                        column: x => x.ResourceAssetId,
                        principalTable: "ResourceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceWorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    MaintenanceType = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReportedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProblemDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VendorReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpectedCompletionAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletionSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PartsRequired = table.Column<bool>(type: "bit", nullable: false),
                    WaitingForPartsSinceUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DowntimeMinutes = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceWorkOrders", x => x.Id);
                    table.CheckConstraint("CK_MaintenanceWorkOrders_AwaitingParts_Date", "[PartsRequired] = 0 OR [WaitingForPartsSinceUtc] IS NOT NULL");
                    table.CheckConstraint("CK_MaintenanceWorkOrders_Dates", "[CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [ReportedAtUtc]");
                    table.CheckConstraint("CK_MaintenanceWorkOrders_Downtime_NonNegative", "[DowntimeMinutes] IS NULL OR [DowntimeMinutes] >= 0");
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_ResourceAssets_ResourceAssetId",
                        column: x => x.ResourceAssetId,
                        principalTable: "ResourceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourcePlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnershipOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalFacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalFacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AssignmentType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourcePlacements", x => x.Id);
                    table.CheckConstraint("CK_ResourcePlacements_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.ForeignKey(
                        name: "FK_ResourcePlacements_Facilities_OperationalFacilityId",
                        column: x => x.OperationalFacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourcePlacements_FacilityUnits_OperationalFacilityId_OperationalFacilityUnitId",
                        columns: x => new { x.OperationalFacilityId, x.OperationalFacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourcePlacements_Organizations_OwnershipOrganizationId",
                        column: x => x.OwnershipOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourcePlacements_ResourceAssets_ResourceAssetId",
                        column: x => x.ResourceAssetId,
                        principalTable: "ResourceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourcePlacements_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceStatusEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RelatedMaintenanceWorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceStatusEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceStatusEvents_ResourceAssets_ResourceAssetId",
                        column: x => x.ResourceAssetId,
                        principalTable: "ResourceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceStatusEvents_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleProfiles",
                columns: table => new
                {
                    ResourceAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlateNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    VehicleIdentificationNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    VehicleCategory = table.Column<int>(type: "int", nullable: false),
                    FuelType = table.Column<int>(type: "int", nullable: true),
                    Odometer = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OdometerRecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RegistrationExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InsuranceExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InspectionExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TrackerInstalled = table.Column<bool>(type: "bit", nullable: false),
                    TrackerExternalId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    OperationalRole = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PassengerCapacity = table.Column<int>(type: "int", nullable: true),
                    PrisonerTransportCapacity = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleProfiles", x => x.ResourceAssetId);
                    table.CheckConstraint("CK_VehicleProfiles_Odometer_NonNegative", "[Odometer] IS NULL OR [Odometer] >= 0");
                    table.CheckConstraint("CK_VehicleProfiles_PassengerCapacity_NonNegative", "[PassengerCapacity] IS NULL OR [PassengerCapacity] >= 0");
                    table.CheckConstraint("CK_VehicleProfiles_PrisonerTransportCapacity_NonNegative", "[PrisonerTransportCapacity] IS NULL OR [PrisonerTransportCapacity] >= 0");
                    table.ForeignKey(
                        name: "FK_VehicleProfiles_ResourceAssets_ResourceAssetId",
                        column: x => x.ResourceAssetId,
                        principalTable: "ResourceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationDeviceProfiles_AssignedUnitId",
                table: "CommunicationDeviceProfiles",
                column: "AssignedUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentProfiles_EquipmentCategory_CalibrationDueAtUtc",
                table: "EquipmentProfiles",
                columns: new[] { "EquipmentCategory", "CalibrationDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentProfiles_EquipmentCategory_InspectionDueAtUtc",
                table: "EquipmentProfiles",
                columns: new[] { "EquipmentCategory", "InspectionDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityAssetProfiles_BuildingId",
                table: "FacilityAssetProfiles",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityAssetProfiles_FacilityUnitId",
                table: "FacilityAssetProfiles",
                column: "FacilityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_AssignedToUserId",
                table: "MaintenanceWorkOrders",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_OrganizationId_WorkOrderNumber",
                table: "MaintenanceWorkOrders",
                columns: new[] { "OrganizationId", "WorkOrderNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_ReportedByUserId",
                table: "MaintenanceWorkOrders",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_ResourceAssetId_Status_ExpectedCompletionAtUtc",
                table: "MaintenanceWorkOrders",
                columns: new[] { "ResourceAssetId", "Status", "ExpectedCompletionAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAssets_CustodianUserId",
                table: "ResourceAssets",
                column: "CustodianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAssets_OperationalFacilityId_OperationalFacilityUnitId",
                table: "ResourceAssets",
                columns: new[] { "OperationalFacilityId", "OperationalFacilityUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAssets_OperationalFacilityId_ResourceType_CurrentStatus",
                table: "ResourceAssets",
                columns: new[] { "OperationalFacilityId", "ResourceType", "CurrentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAssets_OperationalFacilityUnitId_CurrentStatus",
                table: "ResourceAssets",
                columns: new[] { "OperationalFacilityUnitId", "CurrentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAssets_OrganizationId_AssetCode",
                table: "ResourceAssets",
                columns: new[] { "OrganizationId", "AssetCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAssets_OwnershipOrganizationId",
                table: "ResourceAssets",
                column: "OwnershipOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceImportBatches_FacilityId_SourceSystem_SourceReference_FileHash",
                table: "ResourceImportBatches",
                columns: new[] { "FacilityId", "SourceSystem", "SourceReference", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceImportBatches_SubmittedByUserId",
                table: "ResourceImportBatches",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePlacements_AssignedToUserId",
                table: "ResourcePlacements",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePlacements_OperationalFacilityId_OperationalFacilityUnitId_EffectiveFromUtc",
                table: "ResourcePlacements",
                columns: new[] { "OperationalFacilityId", "OperationalFacilityUnitId", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePlacements_OwnershipOrganizationId",
                table: "ResourcePlacements",
                column: "OwnershipOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePlacements_ResourceAssetId",
                table: "ResourcePlacements",
                column: "ResourceAssetId",
                unique: true,
                filter: "[EffectiveToUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRequirements_FacilityId_FacilityUnitId_ResourceType_ResourceCategory_EffectiveFromUtc",
                table: "ResourceRequirements",
                columns: new[] { "FacilityId", "FacilityUnitId", "ResourceType", "ResourceCategory", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRequirements_FacilityOpen",
                table: "ResourceRequirements",
                columns: new[] { "FacilityId", "ResourceType", "ResourceCategory" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [EffectiveToUtc] IS NULL AND [FacilityUnitId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRequirements_OrganizationId",
                table: "ResourceRequirements",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRequirements_UnitOpen",
                table: "ResourceRequirements",
                columns: new[] { "FacilityId", "FacilityUnitId", "ResourceType", "ResourceCategory" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [EffectiveToUtc] IS NULL AND [FacilityUnitId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceStatusEvents_RecordedByUserId",
                table: "ResourceStatusEvents",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceStatusEvents_ResourceAssetId_OccurredAtUtc",
                table: "ResourceStatusEvents",
                columns: new[] { "ResourceAssetId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleProfiles_PlateNumber",
                table: "VehicleProfiles",
                column: "PlateNumber",
                filter: "[PlateNumber] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunicationDeviceProfiles");

            migrationBuilder.DropTable(
                name: "EquipmentProfiles");

            migrationBuilder.DropTable(
                name: "FacilityAssetProfiles");

            migrationBuilder.DropTable(
                name: "MaintenanceWorkOrders");

            migrationBuilder.DropTable(
                name: "ResourceImportBatches");

            migrationBuilder.DropTable(
                name: "ResourcePlacements");

            migrationBuilder.DropTable(
                name: "ResourceRequirements");

            migrationBuilder.DropTable(
                name: "ResourceStatusEvents");

            migrationBuilder.DropTable(
                name: "VehicleProfiles");

            migrationBuilder.DropTable(
                name: "ResourceAssets");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FacilityUnits_FacilityId_Id",
                table: "FacilityUnits");

            migrationBuilder.DropSequence(
                name: "MaintenanceWorkOrderNumberSequence");
        }
    }
}
