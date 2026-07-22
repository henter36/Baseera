using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseC1FormsGovernanceCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OwnerDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    ScopeType = table.Column<int>(type: "int", nullable: false),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedForReviewAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArchivedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_FormDefinitions", x => x.Id);
                    table.CheckConstraint("CK_FormDefinitions_Facility_RequiresFacility", "([ScopeType] <> 3) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)");
                    table.CheckConstraint("CK_FormDefinitions_GlobalHq_NoIds", "([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
                    table.CheckConstraint("CK_FormDefinitions_Region_RequiresRegion", "([ScopeType] <> 2) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
                    table.CheckConstraint("CK_FormDefinitions_Unit_RequiresUnit", "([ScopeType] <> 4) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_FormDefinitions_Departments_OwnerDepartmentId",
                        column: x => x.OwnerDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormDefinitions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormDefinitions_FacilityUnits_FacilityUnitId",
                        column: x => x.FacilityUnitId,
                        principalTable: "FacilityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormDefinitions_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormDefinitions_Users_ArchivedByUserId",
                        column: x => x.ArchivedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormDefinitions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormDefinitions_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormDefinitions_Users_LastModifiedByUserId",
                        column: x => x.LastModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormDefinitions_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormGovernancePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequireReviewBeforeApproval = table.Column<bool>(type: "bit", nullable: false),
                    RequireSeparationOfDuties = table.Column<bool>(type: "bit", nullable: false),
                    AllowDesignerToReviewOwnForm = table.Column<bool>(type: "bit", nullable: false),
                    AllowReviewerToApproveOwnReview = table.Column<bool>(type: "bit", nullable: false),
                    AllowApproverToPublish = table.Column<bool>(type: "bit", nullable: false),
                    DefaultRetentionDays = table.Column<int>(type: "int", nullable: false),
                    SensitiveRetentionDays = table.Column<int>(type: "int", nullable: false),
                    MinimumRetentionDays = table.Column<int>(type: "int", nullable: false),
                    AuditSensitiveViews = table.Column<bool>(type: "bit", nullable: false),
                    AuditExports = table.Column<bool>(type: "bit", nullable: false),
                    RequireReasonForArchive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormGovernancePolicies", x => x.Id);
                    table.CheckConstraint("CK_FormGovernancePolicies_DefaultRetentionDays_NonNegative", "[DefaultRetentionDays] >= 0");
                    table.CheckConstraint("CK_FormGovernancePolicies_MinimumRetentionDays_NonNegative", "[MinimumRetentionDays] >= 0");
                    table.CheckConstraint("CK_FormGovernancePolicies_SensitiveRetentionDays_NonNegative", "[SensitiveRetentionDays] >= 0");
                    table.ForeignKey(
                        name: "FK_FormGovernancePolicies_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormAccessGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrincipalType = table.Column<int>(type: "int", nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Capability = table.Column<int>(type: "int", nullable: false),
                    Effect = table.Column<int>(type: "int", nullable: false),
                    ScopeType = table.Column<int>(type: "int", nullable: true),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopeKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ValidFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ValidToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_FormAccessGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormAccessGrants_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormAccessGrants_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormAccessGrants_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormAccessGrants_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormAccessGrants_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    IsAdministrativeOverride = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormReviewDecisions_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormReviewDecisions_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormAccessGrants_CreatedByUserId",
                table: "FormAccessGrants",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormAccessGrants_FacilityId",
                table: "FormAccessGrants",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_FormAccessGrants_FormDefinitionId_PrincipalType_PrincipalId_Capability_Effect_ScopeKey",
                table: "FormAccessGrants",
                columns: new[] { "FormDefinitionId", "PrincipalType", "PrincipalId", "Capability", "Effect", "ScopeKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FormAccessGrants_RegionId",
                table: "FormAccessGrants",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormAccessGrants_RevokedByUserId",
                table: "FormAccessGrants",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormAccessGrants_ValidToUtc",
                table: "FormAccessGrants",
                column: "ValidToUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_ArchivedByUserId",
                table: "FormDefinitions",
                column: "ArchivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_Classification",
                table: "FormDefinitions",
                column: "Classification");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_Code",
                table: "FormDefinitions",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_CreatedAtUtc",
                table: "FormDefinitions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_CreatedByUserId",
                table: "FormDefinitions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_DeletedByUserId",
                table: "FormDefinitions",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_FacilityId",
                table: "FormDefinitions",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_FacilityUnitId",
                table: "FormDefinitions",
                column: "FacilityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_IsDeleted",
                table: "FormDefinitions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_LastModifiedByUserId",
                table: "FormDefinitions",
                column: "LastModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_OwnerDepartmentId",
                table: "FormDefinitions",
                column: "OwnerDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_RegionId",
                table: "FormDefinitions",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_Status",
                table: "FormDefinitions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_UpdatedByUserId",
                table: "FormDefinitions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormGovernancePolicies_UpdatedByUserId",
                table: "FormGovernancePolicies",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormReviewDecisions_FormDefinitionId",
                table: "FormReviewDecisions",
                column: "FormDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormReviewDecisions_ReviewedAtUtc",
                table: "FormReviewDecisions",
                column: "ReviewedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FormReviewDecisions_ReviewedByUserId",
                table: "FormReviewDecisions",
                column: "ReviewedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormAccessGrants");

            migrationBuilder.DropTable(
                name: "FormGovernancePolicies");

            migrationBuilder.DropTable(
                name: "FormReviewDecisions");

            migrationBuilder.DropTable(
                name: "FormDefinitions");
        }
    }
}
