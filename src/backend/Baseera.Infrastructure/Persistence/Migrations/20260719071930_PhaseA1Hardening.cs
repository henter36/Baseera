using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseA1Hardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProvisioningStatus",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserScopes_Facility_RequiresFacility",
                table: "UserScopes",
                sql: "([ScopeType] NOT IN (3, 6)) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserScopes_GlobalHq_NoIds",
                table: "UserScopes",
                sql: "([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserScopes_Region_RequiresRegion",
                table: "UserScopes",
                sql: "([ScopeType] NOT IN (2, 5)) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserScopes_Unit_RequiresFacilityAndUnit",
                table: "UserScopes",
                sql: "([ScopeType] <> 4) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserScopes_Facility_RequiresFacility",
                table: "UserScopes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserScopes_GlobalHq_NoIds",
                table: "UserScopes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserScopes_Region_RequiresRegion",
                table: "UserScopes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserScopes_Unit_RequiresFacilityAndUnit",
                table: "UserScopes");

            migrationBuilder.DropColumn(
                name: "ProvisioningStatus",
                table: "Users");
        }
    }
}
