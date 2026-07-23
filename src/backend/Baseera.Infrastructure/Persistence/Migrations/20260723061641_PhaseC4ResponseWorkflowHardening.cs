using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseC4ResponseWorkflowHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FormResponseReviewDecisions_SubmissionId_ReviewLevel_ReviewedByUserId_Decision",
                table: "FormResponseReviewDecisions");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseReviewDecisions_ApproveLevel",
                table: "FormResponseReviewDecisions",
                columns: new[] { "ResponseId", "SubmissionId", "ReviewLevel" },
                unique: true,
                filter: "[Decision] = 2");

            // Correct Phase C.4 campaign policy backfill: domain default is RequireSeparationOfDuties = true.
            // Only rows created by the C.4 migration marker are updated; user-created policies are untouched.
            migrationBuilder.Sql(
                """
                UPDATE [FormCampaignResponsePolicies]
                SET [RequireSeparationOfDuties] = 1
                WHERE [CreatedBy] = N'migration:PhaseC4'
                  AND [RequireSeparationOfDuties] = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FormResponseReviewDecisions_ApproveLevel",
                table: "FormResponseReviewDecisions");

            migrationBuilder.CreateIndex(
                name: "IX_FormResponseReviewDecisions_SubmissionId_ReviewLevel_ReviewedByUserId_Decision",
                table: "FormResponseReviewDecisions",
                columns: new[] { "SubmissionId", "ReviewLevel", "ReviewedByUserId", "Decision" },
                unique: true,
                filter: "[Decision] = 2");

            migrationBuilder.Sql(
                """
                UPDATE [FormCampaignResponsePolicies]
                SET [RequireSeparationOfDuties] = 0
                WHERE [CreatedBy] = N'migration:PhaseC4'
                  AND [RequireSeparationOfDuties] = 1;
                """);
        }
    }
}
