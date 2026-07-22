using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseC2VersionedFormDesigner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentLockedVersionId",
                table: "FormDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FormVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BasedOnVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DraftSchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DraftSchemaHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SchemaFormatVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastSavedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedForReviewAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormVersions_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormVersions_FormVersions_BasedOnVersionId",
                        column: x => x.BasedOnVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormVersions_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormVersions_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormSchemaSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaFormatVersion = table.Column<int>(type: "int", nullable: false),
                    CanonicalSchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchemaHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SchemaSizeBytes = table.Column<int>(type: "int", nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: false),
                    SectionCount = table.Column<int>(type: "int", nullable: false),
                    FieldCount = table.Column<int>(type: "int", nullable: false),
                    CalculatedFieldCount = table.Column<int>(type: "int", nullable: false),
                    ConditionCount = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSchemaSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSchemaSnapshots_FormVersions_FormVersionId",
                        column: x => x.FormVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormSchemaSnapshots_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    OwnerDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceFormVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SchemaFormatVersion = table.Column<int>(type: "int", nullable: false),
                    CanonicalSchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchemaHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SchemaSizeBytes = table.Column<int>(type: "int", nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: false),
                    SectionCount = table.Column<int>(type: "int", nullable: false),
                    FieldCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormTemplates_Departments_OwnerDepartmentId",
                        column: x => x.OwnerDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormTemplates_FormDefinitions_SourceFormDefinitionId",
                        column: x => x.SourceFormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormTemplates_FormVersions_SourceFormVersionId",
                        column: x => x.SourceFormVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormTemplates_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormVersionReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    IsAdministrativeOverride = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormVersionReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormVersionReviewDecisions_FormVersions_FormVersionId",
                        column: x => x.FormVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormVersionReviewDecisions_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_CurrentLockedVersionId",
                table: "FormDefinitions",
                column: "CurrentLockedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormSchemaSnapshots_CreatedByUserId",
                table: "FormSchemaSnapshots",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormSchemaSnapshots_FormVersionId",
                table: "FormSchemaSnapshots",
                column: "FormVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormSchemaSnapshots_SchemaHash",
                table: "FormSchemaSnapshots",
                column: "SchemaHash");

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_Category",
                table: "FormTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_Code",
                table: "FormTemplates",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_OwnerDepartmentId",
                table: "FormTemplates",
                column: "OwnerDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_OwnerUserId",
                table: "FormTemplates",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_SourceFormDefinitionId",
                table: "FormTemplates",
                column: "SourceFormDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_SourceFormVersionId",
                table: "FormTemplates",
                column: "SourceFormVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersionReviewDecisions_FormVersionId",
                table: "FormVersionReviewDecisions",
                column: "FormVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersionReviewDecisions_ReviewedAtUtc",
                table: "FormVersionReviewDecisions",
                column: "ReviewedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersionReviewDecisions_ReviewedByUserId",
                table: "FormVersionReviewDecisions",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_ApprovedByUserId",
                table: "FormVersions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_BasedOnVersionId",
                table: "FormVersions",
                column: "BasedOnVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_CreatedByUserId",
                table: "FormVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_FormDefinitionId_VersionNumber",
                table: "FormVersions",
                columns: new[] { "FormDefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_SnapshotId",
                table: "FormVersions",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_Status",
                table: "FormVersions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_UpdatedByUserId",
                table: "FormVersions",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FormDefinitions_FormVersions_CurrentLockedVersionId",
                table: "FormDefinitions",
                column: "CurrentLockedVersionId",
                principalTable: "FormVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[TR_FormSchemaSnapshots_Immutable]', N'TR') IS NULL
BEGIN
    EXEC(N'
    CREATE TRIGGER [dbo].[TR_FormSchemaSnapshots_Immutable]
    ON [dbo].[FormSchemaSnapshots]
    AFTER UPDATE, DELETE
    AS
    BEGIN
        SET NOCOUNT ON;
        THROW 50001, N''FormSchemaSnapshot is immutable and cannot be modified or deleted.'', 1;
    END');
END
""");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[TR_FormSchemaSnapshots_Immutable]', N'TR') IS NOT NULL
    DROP TRIGGER [dbo].[TR_FormSchemaSnapshots_Immutable];
""");

            migrationBuilder.DropForeignKey(
                name: "FK_FormDefinitions_FormVersions_CurrentLockedVersionId",
                table: "FormDefinitions");

            migrationBuilder.DropTable(
                name: "FormSchemaSnapshots");

            migrationBuilder.DropTable(
                name: "FormTemplates");

            migrationBuilder.DropTable(
                name: "FormVersionReviewDecisions");

            migrationBuilder.DropTable(
                name: "FormVersions");

            migrationBuilder.DropIndex(
                name: "IX_FormDefinitions_CurrentLockedVersionId",
                table: "FormDefinitions");

            migrationBuilder.DropColumn(
                name: "CurrentLockedVersionId",
                table: "FormDefinitions");
        }
    }
}
