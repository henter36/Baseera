using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseC2FormVersionCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormDefinitionVersionCounters",
                columns: table => new
                {
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NextVersionNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormDefinitionVersionCounters", x => x.FormDefinitionId);
                    table.ForeignKey(
                        name: "FK_FormDefinitionVersionCounters_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
INSERT INTO [FormDefinitionVersionCounters] ([FormDefinitionId], [NextVersionNumber])
SELECT v.[FormDefinitionId], MAX(v.[VersionNumber]) + 1
FROM [FormVersions] v
GROUP BY v.[FormDefinitionId];
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormDefinitionVersionCounters");
        }
    }
}
