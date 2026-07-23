using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseC4FormResponseWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_FormFacilityAssignments_Id_CampaignId_CycleId_FacilityId",
                table: "FormFacilityAssignments",
                columns: new[] { "Id", "CampaignId", "CycleId", "FacilityId" });

            migrationBuilder.CreateTable(
                name: "FormCampaignResponsePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletionBasis = table.Column<int>(type: "int", nullable: false),
                    ReviewMode = table.Column<int>(type: "int", nullable: false),
                    RequiredApprovalLevels = table.Column<int>(type: "int", nullable: false),
                    AllowLateSubmission = table.Column<bool>(type: "bit", nullable: false),
                    AllowResubmissionAfterReturn = table.Column<bool>(type: "bit", nullable: false),
                    RequireSubmissionAcknowledgement = table.Column<bool>(type: "bit", nullable: false),
                    RequireSeparationOfDuties = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormCampaignResponsePolicies", x => x.Id);
                    table.CheckConstraint("CK_FormCampaignResponsePolicies_Completion", "NOT ([CompletionBasis] = 1 AND [ReviewMode] = 0)");
                    table.CheckConstraint("CK_FormCampaignResponsePolicies_Levels", "([ReviewMode] = 0 AND [RequiredApprovalLevels] = 0) OR ([ReviewMode] = 1 AND [RequiredApprovalLevels] = 1) OR ([ReviewMode] = 2 AND [RequiredApprovalLevels] BETWEEN 2 AND 5)");
                    table.ForeignKey(
                        name: "FK_FormCampaignResponsePolicies_FormCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "FormCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormSchemaSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DraftAnswersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DraftAnswersHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DraftVersion = table.Column<int>(type: "int", nullable: false),
                    CurrentSubmissionNumber = table.Column<int>(type: "int", nullable: false),
                    CurrentReviewLevel = table.Column<int>(type: "int", nullable: false),
                    FirstStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSavedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSavedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReturnedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DueAtUtcOverride = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormResponses", x => x.Id);
                    table.UniqueConstraint("AK_FormResponses_Id_CampaignId_CycleId_FacilityId", x => new { x.Id, x.CampaignId, x.CycleId, x.FacilityId });
                    table.ForeignKey(
                        name: "FK_FormResponses_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponses_FormCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "FormCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponses_FormCycles_CampaignId_CycleId",
                        columns: x => new { x.CampaignId, x.CycleId },
                        principalTable: "FormCycles",
                        principalColumns: new[] { "CampaignId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponses_FormFacilityAssignments_AssignmentId_CampaignId_CycleId_FacilityId",
                        columns: x => new { x.AssignmentId, x.CampaignId, x.CycleId, x.FacilityId },
                        principalTable: "FormFacilityAssignments",
                        principalColumns: new[] { "Id", "CampaignId", "CycleId", "FacilityId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponses_FormSchemaSnapshots_FormSchemaSnapshotId",
                        column: x => x.FormSchemaSnapshotId,
                        principalTable: "FormSchemaSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponses_Users_LastSavedByUserId",
                        column: x => x.LastSavedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponses_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormResponseHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: true),
                    SubmissionNumber = table.Column<int>(type: "int", nullable: true),
                    ReviewLevel = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormResponseHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormResponseHistories_FormResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "FormResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponseHistories_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormResponseMutations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientMutationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliedDraftVersion = table.Column<int>(type: "int", nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResultPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormResponseMutations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormResponseMutations_FormResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "FormResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormResponseSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionNumber = table.Column<int>(type: "int", nullable: false),
                    FormSchemaSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CanonicalAnswersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnswersHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WasLateAtSubmission = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveDueAtSubmissionUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Acknowledged = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgementText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormResponseSubmissions", x => x.Id);
                    table.UniqueConstraint("AK_FormResponseSubmissions_Id_ResponseId", x => new { x.Id, x.ResponseId });
                    table.ForeignKey(
                        name: "FK_FormResponseSubmissions_FormResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "FormResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponseSubmissions_FormSchemaSnapshots_FormSchemaSnapshotId",
                        column: x => x.FormSchemaSnapshotId,
                        principalTable: "FormSchemaSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponseSubmissions_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormResponseReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewLevel = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NewDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormResponseReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormResponseReviewDecisions_FormResponseSubmissions_SubmissionId_ResponseId",
                        columns: x => new { x.SubmissionId, x.ResponseId },
                        principalTable: "FormResponseSubmissions",
                        principalColumns: new[] { "Id", "ResponseId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponseReviewDecisions_FormResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "FormResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponseReviewDecisions_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormResponseReviewComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FieldKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsVisibleToRespondent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormResponseReviewComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormResponseReviewComments_FormResponseReviewDecisions_ReviewDecisionId",
                        column: x => x.ReviewDecisionId,
                        principalTable: "FormResponseReviewDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponseReviewComments_FormResponseSubmissions_SubmissionId_ResponseId",
                        columns: x => new { x.SubmissionId, x.ResponseId },
                        principalTable: "FormResponseSubmissions",
                        principalColumns: new[] { "Id", "ResponseId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponseReviewComments_FormResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "FormResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormResponseReviewComments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormCampaignResponsePolicies_CampaignId",
                table: "FormCampaignResponsePolicies",
                column: "CampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseHistories_ActorUserId",
                table: "FormResponseHistories",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseHistories_ResponseId_OccurredAtUtc",
                table: "FormResponseHistories",
                columns: new[] { "ResponseId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseMutations_ResponseId_ClientMutationId",
                table: "FormResponseMutations",
                columns: new[] { "ResponseId", "ClientMutationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseReviewComments_CreatedByUserId",
                table: "FormResponseReviewComments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseReviewComments_ResponseId_SubmissionId",
                table: "FormResponseReviewComments",
                columns: new[] { "ResponseId", "SubmissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseReviewComments_ReviewDecisionId",
                table: "FormResponseReviewComments",
                column: "ReviewDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseReviewComments_SubmissionId_ResponseId",
                table: "FormResponseReviewComments",
                columns: new[] { "SubmissionId", "ResponseId" });

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseReviewDecisions_ResponseId_ReviewLevel",
                table: "FormResponseReviewDecisions",
                columns: new[] { "ResponseId", "ReviewLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseReviewDecisions_ReviewedByUserId",
                table: "FormResponseReviewDecisions",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseReviewDecisions_SubmissionId_ResponseId",
                table: "FormResponseReviewDecisions",
                columns: new[] { "SubmissionId", "ResponseId" });

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseReviewDecisions_SubmissionId_ReviewLevel_ReviewedByUserId_Decision",
                table: "FormResponseReviewDecisions",
                columns: new[] { "SubmissionId", "ReviewLevel", "ReviewedByUserId", "Decision" },
                unique: true,
                filter: "[Decision] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponses_AssignmentId",
                table: "FormResponses",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormResponses_AssignmentId_CampaignId_CycleId_FacilityId",
                table: "FormResponses",
                columns: new[] { "AssignmentId", "CampaignId", "CycleId", "FacilityId" });

            migrationBuilder.CreateIndex(
                name: "IX_FormResponses_CampaignId_CycleId",
                table: "FormResponses",
                columns: new[] { "CampaignId", "CycleId" });

            migrationBuilder.CreateIndex(
                name: "IX_FormResponses_CampaignId_Status",
                table: "FormResponses",
                columns: new[] { "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FormResponses_CycleId_Status",
                table: "FormResponses",
                columns: new[] { "CycleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FormResponses_FacilityId_Status",
                table: "FormResponses",
                columns: new[] { "FacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FormResponses_FormSchemaSnapshotId",
                table: "FormResponses",
                column: "FormSchemaSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponses_LastSavedByUserId",
                table: "FormResponses",
                column: "LastSavedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponses_SubmittedAtUtc",
                table: "FormResponses",
                column: "SubmittedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponses_SubmittedByUserId",
                table: "FormResponses",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseSubmissions_FormSchemaSnapshotId",
                table: "FormResponseSubmissions",
                column: "FormSchemaSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseSubmissions_ResponseId_SubmissionNumber",
                table: "FormResponseSubmissions",
                columns: new[] { "ResponseId", "SubmissionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseSubmissions_SubmittedByUserId",
                table: "FormResponseSubmissions",
                column: "SubmittedByUserId");

            migrationBuilder.Sql("""
INSERT INTO [FormCampaignResponsePolicies]
    ([Id], [CampaignId], [CompletionBasis], [ReviewMode], [RequiredApprovalLevels],
     [AllowLateSubmission], [AllowResubmissionAfterReturn], [RequireSubmissionAcknowledgement],
     [RequireSeparationOfDuties], [CreatedAtUtc], [CreatedBy])
SELECT
    NEWID(), c.[Id], 0, 0, 0, 1, 1, 0, 0, SYSUTCDATETIME(), N'migration:PhaseC4'
FROM [FormCampaigns] c
WHERE NOT EXISTS (
    SELECT 1 FROM [FormCampaignResponsePolicies] p WHERE p.[CampaignId] = c.[Id]
);
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormCampaignResponsePolicies");

            migrationBuilder.DropTable(
                name: "FormResponseHistories");

            migrationBuilder.DropTable(
                name: "FormResponseMutations");

            migrationBuilder.DropTable(
                name: "FormResponseReviewComments");

            migrationBuilder.DropTable(
                name: "FormResponseReviewDecisions");

            migrationBuilder.DropTable(
                name: "FormResponseSubmissions");

            migrationBuilder.DropTable(
                name: "FormResponses");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FormFacilityAssignments_Id_CampaignId_CycleId_FacilityId",
                table: "FormFacilityAssignments");
        }
    }
}
