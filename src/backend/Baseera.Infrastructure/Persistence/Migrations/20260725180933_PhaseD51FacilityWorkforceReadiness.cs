using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseD51FacilityWorkforceReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    StartLocalTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndLocalTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    CrossesMidnight = table.Column<bool>(type: "bit", nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
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
                    table.PrimaryKey("PK_ShiftDefinitions", x => x.Id);
                    table.CheckConstraint("CK_ShiftDefinitions_CrossesMidnight", "([CrossesMidnight] = 1 AND [EndLocalTime] <= [StartLocalTime]) OR ([CrossesMidnight] = 0 AND [EndLocalTime] > [StartLocalTime])");
                    table.ForeignKey(
                        name: "FK_ShiftDefinitions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftDefinitions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkforceImportBatches",
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
                    table.PrimaryKey("PK_WorkforceImportBatches", x => x.Id);
                    table.CheckConstraint("CK_WorkforceImportBatches_AppliedRows", "[AppliedRows] >= 0 AND [AppliedRows] <= [ValidRows]");
                    table.CheckConstraint("CK_WorkforceImportBatches_ConfirmedState", "([Status] = N'Confirmed' AND [ConfirmedAtUtc] IS NOT NULL) OR ([Status] = N'Previewed' AND [AppliedRows] = 0 AND [ConfirmedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_WorkforceImportBatches_RowTotals", "[ValidRows] + [RejectedRows] + [DuplicateRows] = [TotalRows] AND [TotalRows] >= 0 AND [ValidRows] >= 0 AND [RejectedRows] >= 0 AND [DuplicateRows] >= 0");
                    table.CheckConstraint("CK_WorkforceImportBatches_Status", "[Status] IN (N'Previewed', N'Confirmed')");
                    table.ForeignKey(
                        name: "FK_WorkforceImportBatches_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceImportBatches_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkforceMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalPersonnelId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmploymentStatus = table.Column<int>(type: "int", nullable: false),
                    RankOrGrade = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PrimarySpecialty = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AdministrativeOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HomeFacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentOperationalFacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentOperationalUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupervisorWorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HireDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ServiceStartDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsOperational = table.Column<bool>(type: "bit", nullable: false),
                    IsSensitiveRole = table.Column<bool>(type: "bit", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    LastVerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_WorkforceMembers", x => x.Id);
                    table.CheckConstraint("CK_WorkforceMembers_NoSelfSupervision", "[SupervisorWorkforceMemberId] IS NULL OR [SupervisorWorkforceMemberId] <> [Id]");
                    table.CheckConstraint("CK_WorkforceMembers_UnitRequiresFacility", "[CurrentOperationalUnitId] IS NULL OR [CurrentOperationalFacilityId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_WorkforceMembers_Facilities_CurrentOperationalFacilityId",
                        column: x => x.CurrentOperationalFacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceMembers_Facilities_HomeFacilityId",
                        column: x => x.HomeFacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceMembers_FacilityUnits_CurrentOperationalFacilityId_CurrentOperationalUnitId",
                        columns: x => new { x.CurrentOperationalFacilityId, x.CurrentOperationalUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceMembers_Organizations_AdministrativeOrganizationId",
                        column: x => x.AdministrativeOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceMembers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceMembers_WorkforceMembers_SupervisorWorkforceMemberId",
                        column: x => x.SupervisorWorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkforceRoleDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Criticality = table.Column<int>(type: "int", nullable: false),
                    RequiresCertification = table.Column<bool>(type: "bit", nullable: false),
                    RequiresActiveFitness = table.Column<bool>(type: "bit", nullable: false),
                    RequiresSecurityClearance = table.Column<bool>(type: "bit", nullable: false),
                    CanCoverMultipleUnits = table.Column<bool>(type: "bit", nullable: false),
                    IsShiftBased = table.Column<bool>(type: "bit", nullable: false),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_WorkforceRoleDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkforceRoleDefinitions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DutyRosters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ShiftDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DutyDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_DutyRosters", x => x.Id);
                    table.CheckConstraint("CK_DutyRosters_PublishedState", "([Status] = N'Published' AND [PublishedAtUtc] IS NOT NULL) OR ([Status] = N'Draft' AND [PublishedAtUtc] IS NULL AND [PublishedBy] IS NULL)");
                    table.CheckConstraint("CK_DutyRosters_Status", "[Status] IN (N'Draft', N'Published')");
                    table.ForeignKey(
                        name: "FK_DutyRosters_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyRosters_FacilityUnits_FacilityId_FacilityUnitId",
                        columns: x => new { x.FacilityId, x.FacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyRosters_ShiftDefinitions_ShiftDefinitionId",
                        column: x => x.ShiftDefinitionId,
                        principalTable: "ShiftDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkforceAvailabilityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvailabilityType = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AffectsOperationalAvailability = table.Column<bool>(type: "bit", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    RestrictionCodesCsv = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_WorkforceAvailabilityEvents", x => x.Id);
                    table.CheckConstraint("CK_WorkforceAvailabilityEvents_EffectiveRange", "[EndsAtUtc] IS NULL OR [EndsAtUtc] > [StartsAtUtc]");
                    table.ForeignKey(
                        name: "FK_WorkforceAvailabilityEvents_WorkforceMembers_WorkforceMemberId",
                        column: x => x.WorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CriticalPositionRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoleDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequiredPrimaryCount = table.Column<int>(type: "int", nullable: false),
                    RequiredAlternateCount = table.Column<int>(type: "int", nullable: false),
                    Criticality = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_CriticalPositionRequirements", x => x.Id);
                    table.CheckConstraint("CK_CriticalPositionRequirements_Counts", "[RequiredPrimaryCount] >= 0 AND [RequiredAlternateCount] >= 0");
                    table.CheckConstraint("CK_CriticalPositionRequirements_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.ForeignKey(
                        name: "FK_CriticalPositionRequirements_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriticalPositionRequirements_FacilityUnits_FacilityId_FacilityUnitId",
                        columns: x => new { x.FacilityId, x.FacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriticalPositionRequirements_ShiftDefinitions_ShiftDefinitionId",
                        column: x => x.ShiftDefinitionId,
                        principalTable: "ShiftDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriticalPositionRequirements_WorkforceRoleDefinitions_RoleDefinitionId",
                        column: x => x.RoleDefinitionId,
                        principalTable: "WorkforceRoleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffingRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoleDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequiredHeadcount = table.Column<int>(type: "int", nullable: false),
                    MinimumSafeHeadcount = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_StaffingRequirements", x => x.Id);
                    table.CheckConstraint("CK_StaffingRequirements_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_StaffingRequirements_Quantities", "[RequiredHeadcount] >= 0 AND [MinimumSafeHeadcount] >= 0 AND [MinimumSafeHeadcount] <= [RequiredHeadcount]");
                    table.ForeignKey(
                        name: "FK_StaffingRequirements_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffingRequirements_FacilityUnits_FacilityId_FacilityUnitId",
                        columns: x => new { x.FacilityId, x.FacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffingRequirements_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffingRequirements_ShiftDefinitions_ShiftDefinitionId",
                        column: x => x.ShiftDefinitionId,
                        principalTable: "ShiftDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffingRequirements_WorkforceRoleDefinitions_RoleDefinitionId",
                        column: x => x.RoleDefinitionId,
                        principalTable: "WorkforceRoleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkforceAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoleDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentType = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_WorkforceAssignments", x => x.Id);
                    table.CheckConstraint("CK_WorkforceAssignments_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_WorkforceAssignments_UnitRequiresFacility", "[FacilityUnitId] IS NULL OR [FacilityId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_WorkforceAssignments_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceAssignments_FacilityUnits_FacilityId_FacilityUnitId",
                        columns: x => new { x.FacilityId, x.FacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceAssignments_WorkforceMembers_WorkforceMemberId",
                        column: x => x.WorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceAssignments_WorkforceRoleDefinitions_RoleDefinitionId",
                        column: x => x.RoleDefinitionId,
                        principalTable: "WorkforceRoleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkforceQualifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QualificationType = table.Column<int>(type: "int", nullable: false),
                    RoleDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Issuer = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_WorkforceQualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkforceQualifications_WorkforceMembers_WorkforceMemberId",
                        column: x => x.WorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceQualifications_WorkforceRoleDefinitions_RoleDefinitionId",
                        column: x => x.RoleDefinitionId,
                        principalTable: "WorkforceRoleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkforceReadinessSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ShiftDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoleDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Required = table.Column<int>(type: "int", nullable: false),
                    MinimumSafe = table.Column<int>(type: "int", nullable: false),
                    Assigned = table.Column<int>(type: "int", nullable: false),
                    Scheduled = table.Column<int>(type: "int", nullable: false),
                    Present = table.Column<int>(type: "int", nullable: false),
                    OperationallyAvailable = table.Column<int>(type: "int", nullable: false),
                    Qualified = table.Column<int>(type: "int", nullable: false),
                    Unqualified = table.Column<int>(type: "int", nullable: false),
                    Absent = table.Column<int>(type: "int", nullable: false),
                    OnLeave = table.Column<int>(type: "int", nullable: false),
                    InTraining = table.Column<int>(type: "int", nullable: false),
                    Restricted = table.Column<int>(type: "int", nullable: false),
                    Overtime = table.Column<int>(type: "int", nullable: false),
                    Gap = table.Column<int>(type: "int", nullable: false),
                    SafeGap = table.Column<int>(type: "int", nullable: false),
                    CoverageRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    QualificationCoverage = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    Freshness = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CoverageStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkforceReadinessSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkforceReadinessSnapshots_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceReadinessSnapshots_FacilityUnits_FacilityId_FacilityUnitId",
                        columns: x => new { x.FacilityId, x.FacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceReadinessSnapshots_ShiftDefinitions_ShiftDefinitionId",
                        column: x => x.ShiftDefinitionId,
                        principalTable: "ShiftDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkforceReadinessSnapshots_WorkforceRoleDefinitions_RoleDefinitionId",
                        column: x => x.RoleDefinitionId,
                        principalTable: "WorkforceRoleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DutyRosterAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DutyRosterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CheckInAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CheckOutAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacementForAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_DutyRosterAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DutyRosterAssignments_DutyRosterAssignments_ReplacementForAssignmentId",
                        column: x => x.ReplacementForAssignmentId,
                        principalTable: "DutyRosterAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyRosterAssignments_DutyRosters_DutyRosterId",
                        column: x => x.DutyRosterId,
                        principalTable: "DutyRosters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyRosterAssignments_WorkforceMembers_WorkforceMemberId",
                        column: x => x.WorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyRosterAssignments_WorkforceRoleDefinitions_RoleDefinitionId",
                        column: x => x.RoleDefinitionId,
                        principalTable: "WorkforceRoleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalPositionRequirements_FacilityId_FacilityUnitId",
                table: "CriticalPositionRequirements",
                columns: new[] { "FacilityId", "FacilityUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalPositionRequirements_FacilityId_RoleDefinitionId_ShiftDefinitionId_EffectiveFromUtc",
                table: "CriticalPositionRequirements",
                columns: new[] { "FacilityId", "RoleDefinitionId", "ShiftDefinitionId", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalPositionRequirements_RoleDefinitionId",
                table: "CriticalPositionRequirements",
                column: "RoleDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CriticalPositionRequirements_ShiftDefinitionId",
                table: "CriticalPositionRequirements",
                column: "ShiftDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyRosterAssignments_DutyRosterId_WorkforceMemberId_RoleDefinitionId",
                table: "DutyRosterAssignments",
                columns: new[] { "DutyRosterId", "WorkforceMemberId", "RoleDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyRosterAssignments_ReplacementForAssignmentId",
                table: "DutyRosterAssignments",
                column: "ReplacementForAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyRosterAssignments_RoleDefinitionId",
                table: "DutyRosterAssignments",
                column: "RoleDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyRosterAssignments_WorkforceMemberId",
                table: "DutyRosterAssignments",
                column: "WorkforceMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyRosters_FacilityShiftDate_NoUnit",
                table: "DutyRosters",
                columns: new[] { "FacilityId", "ShiftDefinitionId", "DutyDate" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [FacilityUnitId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DutyRosters_FacilityUnitShiftDate",
                table: "DutyRosters",
                columns: new[] { "FacilityId", "FacilityUnitId", "ShiftDefinitionId", "DutyDate" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [FacilityUnitId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DutyRosters_ShiftDefinitionId",
                table: "DutyRosters",
                column: "ShiftDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDefinitions_FacilityId_Code",
                table: "ShiftDefinitions",
                columns: new[] { "FacilityId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDefinitions_OrganizationId",
                table: "ShiftDefinitions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffingRequirements_FacilityId_FacilityUnitId_RoleDefinitionId_ShiftDefinitionId_EffectiveFromUtc",
                table: "StaffingRequirements",
                columns: new[] { "FacilityId", "FacilityUnitId", "RoleDefinitionId", "ShiftDefinitionId", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffingRequirements_OrganizationId",
                table: "StaffingRequirements",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffingRequirements_RoleDefinitionId",
                table: "StaffingRequirements",
                column: "RoleDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffingRequirements_ShiftDefinitionId",
                table: "StaffingRequirements",
                column: "ShiftDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceAssignments_FacilityId_FacilityUnitId_RoleDefinitionId_EffectiveFromUtc",
                table: "WorkforceAssignments",
                columns: new[] { "FacilityId", "FacilityUnitId", "RoleDefinitionId", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceAssignments_RoleDefinitionId",
                table: "WorkforceAssignments",
                column: "RoleDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceAssignments_WorkforceMemberId_IsPrimary_EffectiveFromUtc",
                table: "WorkforceAssignments",
                columns: new[] { "WorkforceMemberId", "IsPrimary", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceAvailabilityEvents_WorkforceMemberId_StartsAtUtc_EndsAtUtc",
                table: "WorkforceAvailabilityEvents",
                columns: new[] { "WorkforceMemberId", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceImportBatches_FacilityId_SourceSystem_SourceReference_FileHash",
                table: "WorkforceImportBatches",
                columns: new[] { "FacilityId", "SourceSystem", "SourceReference", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceImportBatches_SubmittedByUserId",
                table: "WorkforceImportBatches",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceMembers_AdministrativeOrganizationId",
                table: "WorkforceMembers",
                column: "AdministrativeOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceMembers_CurrentOperationalFacilityId_CurrentOperationalUnitId",
                table: "WorkforceMembers",
                columns: new[] { "CurrentOperationalFacilityId", "CurrentOperationalUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceMembers_CurrentOperationalFacilityId_EmploymentStatus",
                table: "WorkforceMembers",
                columns: new[] { "CurrentOperationalFacilityId", "EmploymentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceMembers_HomeFacilityId",
                table: "WorkforceMembers",
                column: "HomeFacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceMembers_OrganizationId_EmployeeNumber",
                table: "WorkforceMembers",
                columns: new[] { "OrganizationId", "EmployeeNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceMembers_OrganizationId_ExternalPersonnelId",
                table: "WorkforceMembers",
                columns: new[] { "OrganizationId", "ExternalPersonnelId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [ExternalPersonnelId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceMembers_SupervisorWorkforceMemberId",
                table: "WorkforceMembers",
                column: "SupervisorWorkforceMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceMembers_UserId",
                table: "WorkforceMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceQualifications_RoleDefinitionId",
                table: "WorkforceQualifications",
                column: "RoleDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceQualifications_WorkforceMemberId_QualificationType_Status",
                table: "WorkforceQualifications",
                columns: new[] { "WorkforceMemberId", "QualificationType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceReadinessSnapshots_FacilityId_CapturedAtUtc",
                table: "WorkforceReadinessSnapshots",
                columns: new[] { "FacilityId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceReadinessSnapshots_FacilityId_FacilityUnitId_ShiftDefinitionId_RoleDefinitionId_CapturedAtUtc",
                table: "WorkforceReadinessSnapshots",
                columns: new[] { "FacilityId", "FacilityUnitId", "ShiftDefinitionId", "RoleDefinitionId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceReadinessSnapshots_RoleDefinitionId",
                table: "WorkforceReadinessSnapshots",
                column: "RoleDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceReadinessSnapshots_ShiftDefinitionId",
                table: "WorkforceReadinessSnapshots",
                column: "ShiftDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceRoleDefinitions_OrganizationId_Code",
                table: "WorkforceRoleDefinitions",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CriticalPositionRequirements");

            migrationBuilder.DropTable(
                name: "DutyRosterAssignments");

            migrationBuilder.DropTable(
                name: "StaffingRequirements");

            migrationBuilder.DropTable(
                name: "WorkforceAssignments");

            migrationBuilder.DropTable(
                name: "WorkforceAvailabilityEvents");

            migrationBuilder.DropTable(
                name: "WorkforceImportBatches");

            migrationBuilder.DropTable(
                name: "WorkforceQualifications");

            migrationBuilder.DropTable(
                name: "WorkforceReadinessSnapshots");

            migrationBuilder.DropTable(
                name: "DutyRosters");

            migrationBuilder.DropTable(
                name: "WorkforceMembers");

            migrationBuilder.DropTable(
                name: "WorkforceRoleDefinitions");

            migrationBuilder.DropTable(
                name: "ShiftDefinitions");
        }
    }
}
