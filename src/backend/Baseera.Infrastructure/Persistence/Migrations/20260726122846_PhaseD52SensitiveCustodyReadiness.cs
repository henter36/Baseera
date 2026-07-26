using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseD52SensitiveCustodyReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AmmunitionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Caliber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RequiresExpiry = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AmmunitionTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AmmunitionTypes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArmoryLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LocationClassification = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ResponsibleWorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AlternateResponsibleWorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    LastSecurityInspectionAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextSecurityInspectionDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_ArmoryLocations", x => x.Id);
                    table.CheckConstraint("CK_ArmoryLocations_Capacity", "[Capacity] IS NULL OR [Capacity] >= 0");
                    table.ForeignKey(
                        name: "FK_ArmoryLocations_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmoryLocations_FacilityUnits_FacilityId_FacilityUnitId",
                        columns: x => new { x.FacilityId, x.FacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmoryLocations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmoryLocations_WorkforceMembers_AlternateResponsibleWorkforceMemberId",
                        column: x => x.AlternateResponsibleWorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmoryLocations_WorkforceMembers_ResponsibleWorkforceMemberId",
                        column: x => x.ResponsibleWorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SensitiveCustodyImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportKind = table.Column<int>(type: "int", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ValidRows = table.Column<int>(type: "int", nullable: false),
                    RejectedRows = table.Column<int>(type: "int", nullable: false),
                    DuplicateRows = table.Column<int>(type: "int", nullable: false),
                    AppliedRows = table.Column<int>(type: "int", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_SensitiveCustodyImportBatches", x => x.Id);
                    table.CheckConstraint("CK_SensitiveCustodyImportBatches_AppliedRows", "[AppliedRows] >= 0 AND [AppliedRows] <= [ValidRows]");
                    table.CheckConstraint("CK_SensitiveCustodyImportBatches_RowTotals", "[ValidRows] + [RejectedRows] + [DuplicateRows] = [TotalRows] AND [TotalRows] >= 0");
                    table.ForeignKey(
                        name: "FK_SensitiveCustodyImportBatches_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SensitiveCustodyImportBatches_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SensitiveCustodyReconciliationResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResolvedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                    table.PrimaryKey("PK_SensitiveCustodyReconciliationResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SensitiveCustodyReconciliationResolutions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SensitiveCustodyReconciliationResolutions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeaponTypeDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Caliber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsIndividualWeapon = table.Column<bool>(type: "bit", nullable: false),
                    RequiresQualifiedCustodian = table.Column<bool>(type: "bit", nullable: false),
                    InspectionIntervalDays = table.Column<int>(type: "int", nullable: false),
                    MaintenanceIntervalDays = table.Column<int>(type: "int", nullable: true),
                    MinimumSafeCondition = table.Column<int>(type: "int", nullable: false),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_WeaponTypeDefinitions", x => x.Id);
                    table.CheckConstraint("CK_WeaponTypeDefinitions_InspectionInterval", "[InspectionIntervalDays] > 0");
                    table.CheckConstraint("CK_WeaponTypeDefinitions_MaintenanceInterval", "[MaintenanceIntervalDays] IS NULL OR [MaintenanceIntervalDays] > 0");
                    table.ForeignKey(
                        name: "FK_WeaponTypeDefinitions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AmmunitionLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArmoryLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmmunitionTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotNumberEncrypted = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LotNumberHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ManufactureDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiryDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReceivedQuantity = table.Column<int>(type: "int", nullable: false),
                    CurrentQuantity = table.Column<int>(type: "int", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "int", nullable: false),
                    QuarantinedQuantity = table.Column<int>(type: "int", nullable: false),
                    DamagedQuantity = table.Column<int>(type: "int", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
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
                    table.PrimaryKey("PK_AmmunitionLots", x => x.Id);
                    table.CheckConstraint("CK_AmmunitionLots_Available", "[CurrentQuantity] >= [ReservedQuantity] + [QuarantinedQuantity] + [DamagedQuantity]");
                    table.CheckConstraint("CK_AmmunitionLots_Quantities", "[ReceivedQuantity] >= 0 AND [CurrentQuantity] >= 0 AND [ReservedQuantity] >= 0 AND [QuarantinedQuantity] >= 0 AND [DamagedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_AmmunitionLots_AmmunitionTypes_AmmunitionTypeId",
                        column: x => x.AmmunitionTypeId,
                        principalTable: "AmmunitionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AmmunitionLots_ArmoryLocations_ArmoryLocationId",
                        column: x => x.ArmoryLocationId,
                        principalTable: "ArmoryLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AmmunitionLots_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AmmunitionLots_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventorySessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArmoryLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InitiatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    WitnessedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ExpectedWeaponCount = table.Column<int>(type: "int", nullable: false),
                    CountedWeaponCount = table.Column<int>(type: "int", nullable: false),
                    ExpectedAmmunitionQuantity = table.Column<int>(type: "int", nullable: false),
                    CountedAmmunitionQuantity = table.Column<int>(type: "int", nullable: false),
                    DifferenceStatus = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_InventorySessions", x => x.Id);
                    table.CheckConstraint("CK_InventorySessions_CompletedAfterStart", "[CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [StartedAtUtc]");
                    table.CheckConstraint("CK_InventorySessions_Counts", "[ExpectedWeaponCount] >= 0 AND [CountedWeaponCount] >= 0 AND [ExpectedAmmunitionQuantity] >= 0 AND [CountedAmmunitionQuantity] >= 0");
                    table.CheckConstraint("CK_InventorySessions_NoSelfApproval", "[ApprovedBy] IS NULL OR [ApprovedBy] <> [InitiatedBy]");
                    table.ForeignKey(
                        name: "FK_InventorySessions_ArmoryLocations_ArmoryLocationId",
                        column: x => x.ArmoryLocationId,
                        principalTable: "ArmoryLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySessions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySessions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SensitiveResourceRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WeaponTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AmmunitionTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationalRole = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    RequiredQuantity = table.Column<int>(type: "int", nullable: false),
                    MinimumOperationalQuantity = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovalReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
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
                    table.PrimaryKey("PK_SensitiveResourceRequirements", x => x.Id);
                    table.CheckConstraint("CK_SensitiveResourceRequirements_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_SensitiveResourceRequirements_Quantities", "[RequiredQuantity] >= 0 AND [MinimumOperationalQuantity] >= 0 AND [MinimumOperationalQuantity] <= [RequiredQuantity]");
                    table.CheckConstraint("CK_SensitiveResourceRequirements_Target", "([WeaponTypeId] IS NOT NULL AND [AmmunitionTypeId] IS NULL) OR ([WeaponTypeId] IS NULL AND [AmmunitionTypeId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SensitiveResourceRequirements_AmmunitionTypes_AmmunitionTypeId",
                        column: x => x.AmmunitionTypeId,
                        principalTable: "AmmunitionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SensitiveResourceRequirements_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SensitiveResourceRequirements_FacilityUnits_FacilityId_FacilityUnitId",
                        columns: x => new { x.FacilityId, x.FacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SensitiveResourceRequirements_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SensitiveResourceRequirements_WeaponTypeDefinitions_WeaponTypeId",
                        column: x => x.WeaponTypeId,
                        principalTable: "WeaponTypeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AmmunitionTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmmunitionLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmmunitionTransactions", x => x.Id);
                    table.CheckConstraint("CK_AmmunitionTransactions_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_AmmunitionTransactions_AmmunitionLots_AmmunitionLotId",
                        column: x => x.AmmunitionLotId,
                        principalTable: "AmmunitionLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AmmunitionTransactions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AmmunitionTransactions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventorySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    ExpectedReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CountedStatus = table.Column<int>(type: "int", nullable: false),
                    DiscrepancyType = table.Column<int>(type: "int", nullable: false),
                    ExpectedQuantity = table.Column<int>(type: "int", nullable: true),
                    CountedQuantity = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_InventoryEntries", x => x.Id);
                    table.CheckConstraint("CK_InventoryEntries_CountedQuantity", "[CountedQuantity] IS NULL OR [CountedQuantity] >= 0");
                    table.CheckConstraint("CK_InventoryEntries_Quantities", "[ExpectedQuantity] IS NULL OR [ExpectedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryEntries_InventorySessions_InventorySessionId",
                        column: x => x.InventorySessionId,
                        principalTable: "InventorySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustodyTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeaponAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    FromCustodyType = table.Column<int>(type: "int", nullable: false),
                    FromCustodyReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToCustodyType = table.Column<int>(type: "int", nullable: false),
                    ToCustodyReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpectedReturnAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReturnedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PurposeCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ReceivedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    WitnessedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    PreviousTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrectionOfTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustodyTransactions", x => x.Id);
                    table.CheckConstraint("CK_CustodyTransactions_IssueRequiresDestination", "([TransactionType] NOT IN (0, 1, 3, 4)) OR ([ToCustodyType] <> 5 AND [ToCustodyReferenceId] IS NOT NULL)");
                    table.CheckConstraint("CK_CustodyTransactions_NoSelfApproval", "[ApprovedBy] IS NULL OR [ApprovedBy] <> [CreatedBy]");
                    table.CheckConstraint("CK_CustodyTransactions_ReturnedAfterIssue", "[ReturnedAtUtc] IS NULL OR [ReturnedAtUtc] >= [IssuedAtUtc]");
                    table.CheckConstraint("CK_CustodyTransactions_ReturnWindow", "[ExpectedReturnAtUtc] IS NULL OR [ExpectedReturnAtUtc] > [IssuedAtUtc]");
                    table.ForeignKey(
                        name: "FK_CustodyTransactions_CustodyTransactions_CorrectionOfTransactionId",
                        column: x => x.CorrectionOfTransactionId,
                        principalTable: "CustodyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransactions_CustodyTransactions_PreviousTransactionId",
                        column: x => x.PreviousTransactionId,
                        principalTable: "CustodyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransactions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransactions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeaponAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeaponTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalAssetCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SerialNumberEncrypted = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SerialNumberHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Caliber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AcquisitionReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CommissionedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CurrentStatus = table.Column<int>(type: "int", nullable: false),
                    Condition = table.Column<int>(type: "int", nullable: false),
                    Criticality = table.Column<int>(type: "int", nullable: false),
                    CurrentCustodyLocationType = table.Column<int>(type: "int", nullable: false),
                    CurrentFacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentFacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentArmoryLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentCustodyTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastInspectionAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextInspectionDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastVerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
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
                    table.PrimaryKey("PK_WeaponAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeaponAssets_ArmoryLocations_CurrentArmoryLocationId",
                        column: x => x.CurrentArmoryLocationId,
                        principalTable: "ArmoryLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeaponAssets_CustodyTransactions_CurrentCustodyTransactionId",
                        column: x => x.CurrentCustodyTransactionId,
                        principalTable: "CustodyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeaponAssets_Facilities_CurrentFacilityId",
                        column: x => x.CurrentFacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeaponAssets_FacilityUnits_CurrentFacilityId_CurrentFacilityUnitId",
                        columns: x => new { x.CurrentFacilityId, x.CurrentFacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeaponAssets_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeaponAssets_WeaponTypeDefinitions_WeaponTypeId",
                        column: x => x.WeaponTypeId,
                        principalTable: "WeaponTypeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeaponInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeaponAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionType = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    Condition = table.Column<int>(type: "int", nullable: false),
                    Restrictions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InspectorWorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NextDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StatusTransition = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AttachmentReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
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
                    table.PrimaryKey("PK_WeaponInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeaponInspections_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeaponInspections_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeaponInspections_WeaponAssets_WeaponAssetId",
                        column: x => x.WeaponAssetId,
                        principalTable: "WeaponAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeaponInspections_WorkforceMembers_InspectorWorkforceMemberId",
                        column: x => x.InspectorWorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AmmunitionLots_AmmunitionTypeId",
                table: "AmmunitionLots",
                column: "AmmunitionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AmmunitionLots_ArmoryLocationId",
                table: "AmmunitionLots",
                column: "ArmoryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AmmunitionLots_FacilityId_AmmunitionTypeId_ExpiryDateUtc",
                table: "AmmunitionLots",
                columns: new[] { "FacilityId", "AmmunitionTypeId", "ExpiryDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AmmunitionLots_OrganizationId_LotNumberHash",
                table: "AmmunitionLots",
                columns: new[] { "OrganizationId", "LotNumberHash" },
                filter: "[IsDeleted] = 0 AND [LotNumberHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AmmunitionTransactions_AmmunitionLotId",
                table: "AmmunitionTransactions",
                column: "AmmunitionLotId");

            migrationBuilder.CreateIndex(
                name: "IX_AmmunitionTransactions_FacilityId_OccurredAtUtc",
                table: "AmmunitionTransactions",
                columns: new[] { "FacilityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AmmunitionTransactions_OrganizationId",
                table: "AmmunitionTransactions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AmmunitionTypes_OrganizationId_Code",
                table: "AmmunitionTypes",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ArmoryLocations_AlternateResponsibleWorkforceMemberId",
                table: "ArmoryLocations",
                column: "AlternateResponsibleWorkforceMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmoryLocations_FacilityId_Code",
                table: "ArmoryLocations",
                columns: new[] { "FacilityId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ArmoryLocations_FacilityId_FacilityUnitId",
                table: "ArmoryLocations",
                columns: new[] { "FacilityId", "FacilityUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArmoryLocations_OrganizationId",
                table: "ArmoryLocations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmoryLocations_ResponsibleWorkforceMemberId",
                table: "ArmoryLocations",
                column: "ResponsibleWorkforceMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransactions_CorrectionOfTransactionId",
                table: "CustodyTransactions",
                column: "CorrectionOfTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransactions_FacilityId_Status_ExpectedReturnAtUtc",
                table: "CustodyTransactions",
                columns: new[] { "FacilityId", "Status", "ExpectedReturnAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransactions_OrganizationId",
                table: "CustodyTransactions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransactions_PreviousTransactionId",
                table: "CustodyTransactions",
                column: "PreviousTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransactions_WeaponAssetId",
                table: "CustodyTransactions",
                column: "WeaponAssetId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryEntries_InventorySessionId_DiscrepancyType_ResolvedAtUtc",
                table: "InventoryEntries",
                columns: new[] { "InventorySessionId", "DiscrepancyType", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventorySessions_ArmoryLocationId",
                table: "InventorySessions",
                column: "ArmoryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySessions_FacilityId_Status_StartedAtUtc",
                table: "InventorySessions",
                columns: new[] { "FacilityId", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventorySessions_OrganizationId",
                table: "InventorySessions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveCustodyImportBatches_FacilityId_ImportKind_FileHash",
                table: "SensitiveCustodyImportBatches",
                columns: new[] { "FacilityId", "ImportKind", "FileHash" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveCustodyImportBatches_OrganizationId",
                table: "SensitiveCustodyImportBatches",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveCustodyReconciliationResolutions_FacilityId_ItemKey",
                table: "SensitiveCustodyReconciliationResolutions",
                columns: new[] { "FacilityId", "ItemKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveCustodyReconciliationResolutions_OrganizationId",
                table: "SensitiveCustodyReconciliationResolutions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveResourceRequirements_AmmunitionTypeId",
                table: "SensitiveResourceRequirements",
                column: "AmmunitionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveResourceRequirements_FacilityId_FacilityUnitId",
                table: "SensitiveResourceRequirements",
                columns: new[] { "FacilityId", "FacilityUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveResourceRequirements_FacilityId_WeaponTypeId_AmmunitionTypeId_EffectiveFromUtc",
                table: "SensitiveResourceRequirements",
                columns: new[] { "FacilityId", "WeaponTypeId", "AmmunitionTypeId", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveResourceRequirements_OrganizationId",
                table: "SensitiveResourceRequirements",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveResourceRequirements_WeaponTypeId",
                table: "SensitiveResourceRequirements",
                column: "WeaponTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WeaponAssets_CurrentArmoryLocationId",
                table: "WeaponAssets",
                column: "CurrentArmoryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_WeaponAssets_CurrentCustodyTransactionId",
                table: "WeaponAssets",
                column: "CurrentCustodyTransactionId",
                unique: true,
                filter: "[CurrentCustodyTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WeaponAssets_CurrentFacilityId_CurrentFacilityUnitId",
                table: "WeaponAssets",
                columns: new[] { "CurrentFacilityId", "CurrentFacilityUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_WeaponAssets_CurrentFacilityId_CurrentStatus_NextInspectionDueAtUtc",
                table: "WeaponAssets",
                columns: new[] { "CurrentFacilityId", "CurrentStatus", "NextInspectionDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WeaponAssets_OrganizationId_InternalAssetCode",
                table: "WeaponAssets",
                columns: new[] { "OrganizationId", "InternalAssetCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WeaponAssets_OrganizationId_SerialNumberHash",
                table: "WeaponAssets",
                columns: new[] { "OrganizationId", "SerialNumberHash" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WeaponAssets_WeaponTypeId",
                table: "WeaponAssets",
                column: "WeaponTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WeaponInspections_FacilityId_NextDueAtUtc",
                table: "WeaponInspections",
                columns: new[] { "FacilityId", "NextDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WeaponInspections_InspectorWorkforceMemberId",
                table: "WeaponInspections",
                column: "InspectorWorkforceMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_WeaponInspections_OrganizationId",
                table: "WeaponInspections",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WeaponInspections_WeaponAssetId",
                table: "WeaponInspections",
                column: "WeaponAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WeaponTypeDefinitions_OrganizationId_Code",
                table: "WeaponTypeDefinitions",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_CustodyTransactions_WeaponAssets_WeaponAssetId",
                table: "CustodyTransactions",
                column: "WeaponAssetId",
                principalTable: "WeaponAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeaponAssets_ArmoryLocations_CurrentArmoryLocationId",
                table: "WeaponAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodyTransactions_WeaponAssets_WeaponAssetId",
                table: "CustodyTransactions");

            migrationBuilder.DropTable(
                name: "AmmunitionTransactions");

            migrationBuilder.DropTable(
                name: "InventoryEntries");

            migrationBuilder.DropTable(
                name: "SensitiveCustodyImportBatches");

            migrationBuilder.DropTable(
                name: "SensitiveCustodyReconciliationResolutions");

            migrationBuilder.DropTable(
                name: "SensitiveResourceRequirements");

            migrationBuilder.DropTable(
                name: "WeaponInspections");

            migrationBuilder.DropTable(
                name: "AmmunitionLots");

            migrationBuilder.DropTable(
                name: "InventorySessions");

            migrationBuilder.DropTable(
                name: "AmmunitionTypes");

            migrationBuilder.DropTable(
                name: "ArmoryLocations");

            migrationBuilder.DropTable(
                name: "WeaponAssets");

            migrationBuilder.DropTable(
                name: "CustodyTransactions");

            migrationBuilder.DropTable(
                name: "WeaponTypeDefinitions");
        }
    }
}
