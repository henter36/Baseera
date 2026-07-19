using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseA1FilteredIndexesAndRestrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Buildings_Facilities_FacilityId",
                table: "Buildings");

            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Organizations_OrganizationId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_Facilities_Regions_RegionId",
                table: "Facilities");

            migrationBuilder.DropForeignKey(
                name: "FK_FacilityAssetLocations_Buildings_BuildingId",
                table: "FacilityAssetLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_FacilityUnits_Facilities_FacilityId",
                table: "FacilityUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_Regions_Organizations_OrganizationId",
                table: "Regions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserScopes_Users_UserId",
                table: "UserScopes");

            migrationBuilder.DropIndex(
                name: "IX_UserScopes_UserId",
                table: "UserScopes");

            migrationBuilder.DropIndex(
                name: "IX_Users_ExternalSubject",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_UserName",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Code",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Regions_Code",
                table: "Regions");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_Code",
                table: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_FacilityUnits_FacilityId_Code",
                table: "FacilityUnits");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_Code",
                table: "Facilities");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_Facilities_FacilityId",
                table: "Buildings",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Organizations_OrganizationId",
                table: "Departments",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Facilities_Regions_RegionId",
                table: "Facilities",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityAssetLocations_Buildings_BuildingId",
                table: "FacilityAssetLocations",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityUnits_Facilities_FacilityId",
                table: "FacilityUnits",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Regions_Organizations_OrganizationId",
                table: "Regions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserScopes_Users_UserId",
                table: "UserScopes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Buildings_Facilities_FacilityId",
                table: "Buildings");

            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Organizations_OrganizationId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_Facilities_Regions_RegionId",
                table: "Facilities");

            migrationBuilder.DropForeignKey(
                name: "FK_FacilityAssetLocations_Buildings_BuildingId",
                table: "FacilityAssetLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_FacilityUnits_Facilities_FacilityId",
                table: "FacilityUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_Regions_Organizations_OrganizationId",
                table: "Regions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserScopes_Users_UserId",
                table: "UserScopes");

            migrationBuilder.DropIndex(
                name: "IX_UserScopes_UserId_ScopeType_RegionId_FacilityId_FacilityUnitId",
                table: "UserScopes");

            migrationBuilder.DropIndex(
                name: "IX_Users_ExternalSubject",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_UserName",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Code",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Regions_Code",
                table: "Regions");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_Code",
                table: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_FacilityUnits_FacilityId_Code",
                table: "FacilityUnits");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_Code",
                table: "Facilities");

            migrationBuilder.CreateIndex(
                name: "IX_UserScopes_UserId",
                table: "UserScopes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalSubject",
                table: "Users",
                column: "ExternalSubject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regions_Code",
                table: "Regions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Code",
                table: "Organizations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityUnits_FacilityId_Code",
                table: "FacilityUnits",
                columns: new[] { "FacilityId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_Code",
                table: "Facilities",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_Facilities_FacilityId",
                table: "Buildings",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Organizations_OrganizationId",
                table: "Departments",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Facilities_Regions_RegionId",
                table: "Facilities",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityAssetLocations_Buildings_BuildingId",
                table: "FacilityAssetLocations",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityUnits_Facilities_FacilityId",
                table: "FacilityUnits",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Regions_Organizations_OrganizationId",
                table: "Regions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserScopes_Users_UserId",
                table: "UserScopes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
