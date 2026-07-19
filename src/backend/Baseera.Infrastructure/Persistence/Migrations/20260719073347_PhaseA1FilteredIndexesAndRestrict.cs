using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseA1FilteredIndexesAndRestrict : Migration
    {
        private static readonly (string Name, string Table)[] RestrictForeignKeys =
        [
            ("FK_Buildings_Facilities_FacilityId", "Buildings"),
            ("FK_Departments_Organizations_OrganizationId", "Departments"),
            ("FK_Facilities_Regions_RegionId", "Facilities"),
            ("FK_FacilityAssetLocations_Buildings_BuildingId", "FacilityAssetLocations"),
            ("FK_FacilityUnits_Facilities_FacilityId", "FacilityUnits"),
            ("FK_Regions_Organizations_OrganizationId", "Regions"),
            ("FK_UserScopes_Users_UserId", "UserScopes")
        ];

        private static readonly (string Name, string Table, string Column, string PrincipalTable)[] ForeignKeyDefs =
        [
            ("FK_Buildings_Facilities_FacilityId", "Buildings", "FacilityId", "Facilities"),
            ("FK_Departments_Organizations_OrganizationId", "Departments", "OrganizationId", "Organizations"),
            ("FK_Facilities_Regions_RegionId", "Facilities", "RegionId", "Regions"),
            ("FK_FacilityAssetLocations_Buildings_BuildingId", "FacilityAssetLocations", "BuildingId", "Buildings"),
            ("FK_FacilityUnits_Facilities_FacilityId", "FacilityUnits", "FacilityId", "Facilities"),
            ("FK_Regions_Organizations_OrganizationId", "Regions", "OrganizationId", "Organizations"),
            ("FK_UserScopes_Users_UserId", "UserScopes", "UserId", "Users")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropListedForeignKeys(migrationBuilder);
            DropPreFilterIndexes(migrationBuilder);
            CreateFilteredIndexes(migrationBuilder);
            AddListedForeignKeys(migrationBuilder, ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropListedForeignKeys(migrationBuilder);
            DropFilteredIndexes(migrationBuilder);
            CreatePreFilterIndexes(migrationBuilder);
            AddListedForeignKeys(migrationBuilder, ReferentialAction.Cascade);
        }

        private static void DropListedForeignKeys(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, table) in RestrictForeignKeys)
            {
                migrationBuilder.DropForeignKey(name: name, table: table);
            }
        }

        private static void AddListedForeignKeys(MigrationBuilder migrationBuilder, ReferentialAction onDelete)
        {
            foreach (var (name, table, column, principalTable) in ForeignKeyDefs)
            {
                migrationBuilder.AddForeignKey(
                    name: name,
                    table: table,
                    column: column,
                    principalTable: principalTable,
                    principalColumn: "Id",
                    onDelete: onDelete);
            }
        }

        private static void DropPreFilterIndexes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_UserScopes_UserId", table: "UserScopes");
            migrationBuilder.DropIndex(name: "IX_Users_ExternalSubject", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_UserName", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Roles_Code", table: "Roles");
            migrationBuilder.DropIndex(name: "IX_Regions_Code", table: "Regions");
            migrationBuilder.DropIndex(name: "IX_Organizations_Code", table: "Organizations");
            migrationBuilder.DropIndex(name: "IX_FacilityUnits_FacilityId_Code", table: "FacilityUnits");
            migrationBuilder.DropIndex(name: "IX_Facilities_Code", table: "Facilities");
        }

        private static void CreateFilteredIndexes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserScopes_UserId_ScopeType_RegionId_FacilityId_FacilityUnitId",
                table: "UserScopes",
                columns: new[] { "UserId", "ScopeType", "RegionId", "FacilityId", "FacilityUnitId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalSubject",
                table: "Users",
                column: "ExternalSubject",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                table: "Roles",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Regions_Code",
                table: "Regions",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Code",
                table: "Organizations",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityUnits_FacilityId_Code",
                table: "FacilityUnits",
                columns: new[] { "FacilityId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_Code",
                table: "Facilities",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        private static void DropFilteredIndexes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserScopes_UserId_ScopeType_RegionId_FacilityId_FacilityUnitId",
                table: "UserScopes");
            migrationBuilder.DropIndex(name: "IX_Users_ExternalSubject", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_UserName", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Roles_Code", table: "Roles");
            migrationBuilder.DropIndex(name: "IX_Regions_Code", table: "Regions");
            migrationBuilder.DropIndex(name: "IX_Organizations_Code", table: "Organizations");
            migrationBuilder.DropIndex(name: "IX_FacilityUnits_FacilityId_Code", table: "FacilityUnits");
            migrationBuilder.DropIndex(name: "IX_Facilities_Code", table: "Facilities");
        }

        private static void CreatePreFilterIndexes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(name: "IX_UserScopes_UserId", table: "UserScopes", column: "UserId");
            migrationBuilder.CreateIndex(name: "IX_Users_ExternalSubject", table: "Users", column: "ExternalSubject", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Users_UserName", table: "Users", column: "UserName", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Roles_Code", table: "Roles", column: "Code", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Regions_Code", table: "Regions", column: "Code", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Organizations_Code", table: "Organizations", column: "Code", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_FacilityUnits_FacilityId_Code",
                table: "FacilityUnits",
                columns: new[] { "FacilityId", "Code" },
                unique: true);
            migrationBuilder.CreateIndex(name: "IX_Facilities_Code", table: "Facilities", column: "Code", unique: true);
        }
    }
}
