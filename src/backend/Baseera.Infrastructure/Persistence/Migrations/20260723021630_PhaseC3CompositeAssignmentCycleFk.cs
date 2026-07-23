using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseC3CompositeAssignmentCycleFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormFacilityAssignments_FormCycles_CycleId",
                table: "FormFacilityAssignments");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_FormCycles_CampaignId_Id",
                table: "FormCycles",
                columns: new[] { "CampaignId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FormFacilityAssignments_CampaignId_CycleId",
                table: "FormFacilityAssignments",
                columns: new[] { "CampaignId", "CycleId" });

            migrationBuilder.AddForeignKey(
                name: "FK_FormFacilityAssignments_FormCycles_CampaignId_CycleId",
                table: "FormFacilityAssignments",
                columns: new[] { "CampaignId", "CycleId" },
                principalTable: "FormCycles",
                principalColumns: new[] { "CampaignId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormFacilityAssignments_FormCycles_CampaignId_CycleId",
                table: "FormFacilityAssignments");

            migrationBuilder.DropIndex(
                name: "IX_FormFacilityAssignments_CampaignId_CycleId",
                table: "FormFacilityAssignments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FormCycles_CampaignId_Id",
                table: "FormCycles");

            migrationBuilder.AddForeignKey(
                name: "FK_FormFacilityAssignments_FormCycles_CycleId",
                table: "FormFacilityAssignments",
                column: "CycleId",
                principalTable: "FormCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
