using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseC3FormPublishingScheduler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormSchemaSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecurrenceKind = table.Column<int>(type: "int", nullable: false),
                    RecurrenceConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstOpenAtLocal = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResponseWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    GracePeriodMinutes = table.Column<int>(type: "int", nullable: false),
                    CloseAfterMinutes = table.Column<int>(type: "int", nullable: false),
                    BusinessDayAdjustment = table.Column<int>(type: "int", nullable: false),
                    NextOccurrenceUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastGeneratedOccurrenceUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PausedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PausedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PauseReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_FormCampaigns", x => x.Id);
                    table.CheckConstraint("CK_FormCampaigns_CloseAfterMinutes", "[CloseAfterMinutes] >= 0");
                    table.CheckConstraint("CK_FormCampaigns_GracePeriodMinutes", "[GracePeriodMinutes] >= 0");
                    table.CheckConstraint("CK_FormCampaigns_ResponseWindowMinutes", "[ResponseWindowMinutes] > 0");
                    table.ForeignKey(
                        name: "FK_FormCampaigns_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaigns_FormSchemaSnapshots_FormSchemaSnapshotId",
                        column: x => x.FormSchemaSnapshotId,
                        principalTable: "FormSchemaSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaigns_FormVersions_FormVersionId",
                        column: x => x.FormVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaigns_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaigns_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaigns_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaigns_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaigns_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaigns_Users_PausedByUserId",
                        column: x => x.PausedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaigns_Users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaigns_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationBusinessCalendarDates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsWorkingDayOverride = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationBusinessCalendarDates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationBusinessCalendarDates_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationBusinessCalendarDates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormCampaignExclusions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormCampaignExclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormCampaignExclusions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaignExclusions_FormCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "FormCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCampaignExclusions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    OccurrenceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScheduledOccurrenceLocal = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScheduledOccurrenceUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OpenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GraceEndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CloseAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FormVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormSchemaSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AssignedFacilityCount = table.Column<int>(type: "int", nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormCycles_FormCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "FormCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCycles_FormSchemaSnapshots_FormSchemaSnapshotId",
                        column: x => x.FormSchemaSnapshotId,
                        principalTable: "FormSchemaSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormCycles_FormVersions_FormVersionId",
                        column: x => x.FormVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormTargetRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleType = table.Column<int>(type: "int", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormTargetRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormTargetRules_FormCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "FormCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormTargetRules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormFacilityAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegionIdAtAssignment = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityCodeAtAssignment = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FacilityNameArAtAssignment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegionNameArAtAssignment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FacilityTypeAtAssignment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetRuleType = table.Column<int>(type: "int", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    UnavailableReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormFacilityAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormFacilityAssignments_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormFacilityAssignments_FormCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "FormCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormFacilityAssignments_FormCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "FormCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaignExclusions_CampaignId_FacilityId",
                table: "FormCampaignExclusions",
                columns: new[] { "CampaignId", "FacilityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaignExclusions_CreatedByUserId",
                table: "FormCampaignExclusions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaignExclusions_FacilityId",
                table: "FormCampaignExclusions",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_CancelledByUserId",
                table: "FormCampaigns",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_ClosedByUserId",
                table: "FormCampaigns",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_CreatedByUserId",
                table: "FormCampaigns",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_DeletedByUserId",
                table: "FormCampaigns",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_FormDefinitionId",
                table: "FormCampaigns",
                column: "FormDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_FormSchemaSnapshotId",
                table: "FormCampaigns",
                column: "FormSchemaSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_FormVersionId",
                table: "FormCampaigns",
                column: "FormVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_OrganizationId_Code",
                table: "FormCampaigns",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_PausedByUserId",
                table: "FormCampaigns",
                column: "PausedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_PublishedByUserId",
                table: "FormCampaigns",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_Status_NextOccurrenceUtc",
                table: "FormCampaigns",
                columns: new[] { "Status", "NextOccurrenceUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaigns_UpdatedByUserId",
                table: "FormCampaigns",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCycles_CampaignId_OccurrenceKey",
                table: "FormCycles",
                columns: new[] { "CampaignId", "OccurrenceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormCycles_CampaignId_SequenceNumber",
                table: "FormCycles",
                columns: new[] { "CampaignId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormCycles_CloseAtUtc",
                table: "FormCycles",
                column: "CloseAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FormCycles_DueAtUtc",
                table: "FormCycles",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FormCycles_FormSchemaSnapshotId",
                table: "FormCycles",
                column: "FormSchemaSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCycles_FormVersionId",
                table: "FormCycles",
                column: "FormVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormCycles_Status_OpenAtUtc",
                table: "FormCycles",
                columns: new[] { "Status", "OpenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FormFacilityAssignments_CampaignId",
                table: "FormFacilityAssignments",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_FormFacilityAssignments_CycleId_FacilityId",
                table: "FormFacilityAssignments",
                columns: new[] { "CycleId", "FacilityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormFacilityAssignments_FacilityId",
                table: "FormFacilityAssignments",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_FormFacilityAssignments_RegionIdAtAssignment",
                table: "FormFacilityAssignments",
                column: "RegionIdAtAssignment");

            migrationBuilder.CreateIndex(
                name: "IX_FormTargetRules_CampaignId",
                table: "FormTargetRules",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_FormTargetRules_CreatedByUserId",
                table: "FormTargetRules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationBusinessCalendarDates_CreatedByUserId",
                table: "OrganizationBusinessCalendarDates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationBusinessCalendarDates_OrganizationId_LocalDate",
                table: "OrganizationBusinessCalendarDates",
                columns: new[] { "OrganizationId", "LocalDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormCampaignExclusions");

            migrationBuilder.DropTable(
                name: "FormFacilityAssignments");

            migrationBuilder.DropTable(
                name: "FormTargetRules");

            migrationBuilder.DropTable(
                name: "OrganizationBusinessCalendarDates");

            migrationBuilder.DropTable(
                name: "FormCycles");

            migrationBuilder.DropTable(
                name: "FormCampaigns");
        }
    }
}
