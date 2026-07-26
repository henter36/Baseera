using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseD51WorkforceReconciliationExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkforceImportBatches_FacilityId_SourceSystem_SourceReference_FileHash",
                table: "WorkforceImportBatches");

            migrationBuilder.AddColumn<int>(
                name: "ImportKind",
                table: "WorkforceImportBatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WorkforceReconciliationResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssueType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ResolutionAction = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkforceReconciliationResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkforceReconciliationResolutions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceImportBatches_FacilityId_ImportKind_SourceSystem_SourceReference_FileHash",
                table: "WorkforceImportBatches",
                columns: new[] { "FacilityId", "ImportKind", "SourceSystem", "SourceReference", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceReconciliationResolutions_FacilityId_ItemKey",
                table: "WorkforceReconciliationResolutions",
                columns: new[] { "FacilityId", "ItemKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkforceReconciliationResolutions");

            migrationBuilder.DropIndex(
                name: "IX_WorkforceImportBatches_FacilityId_ImportKind_SourceSystem_SourceReference_FileHash",
                table: "WorkforceImportBatches");

            migrationBuilder.DropColumn(
                name: "ImportKind",
                table: "WorkforceImportBatches");

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceImportBatches_FacilityId_SourceSystem_SourceReference_FileHash",
                table: "WorkforceImportBatches",
                columns: new[] { "FacilityId", "SourceSystem", "SourceReference", "FileHash" },
                unique: true);
        }
    }
}
