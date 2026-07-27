using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleInProgressRiskAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_RiskAssessments_RiskRecordId_AssessmentType_InProgress",
                table: "RiskAssessments",
                columns: new[] { "RiskRecordId", "AssessmentType" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN (0, 1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_RiskAssessments_RiskRecordId_AssessmentType_InProgress",
                table: "RiskAssessments");
        }
    }
}
