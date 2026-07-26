using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseD51WorkforceIntegrityInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "CapturedDateUtc",
                table: "WorkforceReadinessSnapshots",
                type: "date",
                nullable: false,
                defaultValueSql: "CONVERT(date, SYSUTCDATETIME())");

            migrationBuilder.Sql(
                """
                UPDATE [WorkforceReadinessSnapshots]
                SET [CapturedDateUtc] = CONVERT(date, SWITCHOFFSET([CapturedAtUtc], '+00:00'))
                WHERE [CapturedAtUtc] IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                WITH RankedSnapshots AS (
                    SELECT
                        [Id],
                        ROW_NUMBER() OVER (
                            PARTITION BY [FacilityId], [FacilityUnitId], [ShiftDefinitionId], [RoleDefinitionId], [CapturedDateUtc]
                            ORDER BY [CapturedAtUtc] DESC, [Id] DESC
                        ) AS [RowNumber]
                    FROM [WorkforceReadinessSnapshots]
                )
                DELETE FROM RankedSnapshots
                WHERE [RowNumber] > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceReadinessSnapshots_FacilityScopeDate",
                table: "WorkforceReadinessSnapshots",
                columns: new[] { "FacilityId", "FacilityUnitId", "ShiftDefinitionId", "RoleDefinitionId", "CapturedDateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DutyRosterAssignments_RosterMember_Active",
                table: "DutyRosterAssignments",
                columns: new[] { "DutyRosterId", "WorkforceMemberId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] <> 6 AND [Status] <> 8");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkforceReadinessSnapshots_FacilityScopeDate",
                table: "WorkforceReadinessSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_DutyRosterAssignments_RosterMember_Active",
                table: "DutyRosterAssignments");

            migrationBuilder.DropColumn(
                name: "CapturedDateUtc",
                table: "WorkforceReadinessSnapshots");
        }
    }
}
