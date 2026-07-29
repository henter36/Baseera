using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1BObservationTriageApprovalPartsSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClosureReason",
                table: "OperationalNotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DuplicateOfNoteId",
                table: "OperationalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoActionJustificationAr",
                table: "OperationalNotes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TreatmentExecutionType",
                table: "OperationalNotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TreatmentResultText",
                table: "OperationalNotes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TreatmentResultType",
                table: "OperationalNotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TriageDecidedAtUtc",
                table: "OperationalNotes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TriageDecidedByUserId",
                table: "OperationalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TriageOutcome",
                table: "OperationalNotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsPartsWorkflow",
                table: "NoteTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "NoteDecisionApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    JustificationAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OriginalNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProposedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteDecisionApprovals", x => x.Id);
                    table.CheckConstraint("CK_NoteDecisionApprovals_Duplicate_RequiresOriginal", "([DecisionType] <> 1) OR ([OriginalNoteId] IS NOT NULL)");
                    table.CheckConstraint("CK_NoteDecisionApprovals_NoSelfApproval", "[ReviewedByUserId] IS NULL OR [ReviewedByUserId] <> [ProposedByUserId]");
                    table.ForeignKey(
                        name: "FK_NoteDecisionApprovals_OperationalNotes_OperationalNoteId",
                        column: x => x.OperationalNoteId,
                        principalTable: "OperationalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteDecisionApprovals_OperationalNotes_OriginalNoteId",
                        column: x => x.OriginalNoteId,
                        principalTable: "OperationalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteDecisionApprovals_Users_ProposedByUserId",
                        column: x => x.ProposedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteDecisionApprovals_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotePartsRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InstalledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SupplierOrSource = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotePartsRequirements", x => x.Id);
                    table.CheckConstraint("CK_NotePartsRequirements_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_NotePartsRequirements_OperationalNotes_OperationalNoteId",
                        column: x => x.OperationalNoteId,
                        principalTable: "OperationalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotePartsRequirements_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotePartsRequirements_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteSlaPausePeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RelatedPartsRequirementIdsCsv = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteSlaPausePeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteSlaPausePeriods_OperationalNotes_OperationalNoteId",
                        column: x => x.OperationalNoteId,
                        principalTable: "OperationalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteSlaPausePeriods_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteSlaPausePeriods_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_DuplicateOfNoteId",
                table: "OperationalNotes",
                column: "DuplicateOfNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_TriageDecidedByUserId",
                table: "OperationalNotes",
                column: "TriageDecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_TriageOutcome",
                table: "OperationalNotes",
                column: "TriageOutcome");

            migrationBuilder.CreateIndex(
                name: "IX_NoteDecisionApprovals_OperationalNoteId_DecisionType",
                table: "NoteDecisionApprovals",
                columns: new[] { "OperationalNoteId", "DecisionType" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NoteDecisionApprovals_OperationalNoteId_ProposedAtUtc",
                table: "NoteDecisionApprovals",
                columns: new[] { "OperationalNoteId", "ProposedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteDecisionApprovals_OriginalNoteId",
                table: "NoteDecisionApprovals",
                column: "OriginalNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteDecisionApprovals_ProposedByUserId",
                table: "NoteDecisionApprovals",
                column: "ProposedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteDecisionApprovals_ReviewedByUserId",
                table: "NoteDecisionApprovals",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotePartsRequirements_CancelledByUserId",
                table: "NotePartsRequirements",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotePartsRequirements_CreatedByUserId",
                table: "NotePartsRequirements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotePartsRequirements_OperationalNoteId_ItemCode",
                table: "NotePartsRequirements",
                columns: new[] { "OperationalNoteId", "ItemCode" },
                unique: true,
                filter: "[ItemCode] IS NOT NULL AND [Status] <> 5");

            migrationBuilder.CreateIndex(
                name: "IX_NotePartsRequirements_OperationalNoteId_Status",
                table: "NotePartsRequirements",
                columns: new[] { "OperationalNoteId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteSlaPausePeriods_ApprovedByUserId",
                table: "NoteSlaPausePeriods",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteSlaPausePeriods_OperationalNoteId",
                table: "NoteSlaPausePeriods",
                column: "OperationalNoteId",
                unique: true,
                filter: "[EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NoteSlaPausePeriods_RequestedByUserId",
                table: "NoteSlaPausePeriods",
                column: "RequestedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationalNotes_OperationalNotes_DuplicateOfNoteId",
                table: "OperationalNotes",
                column: "DuplicateOfNoteId",
                principalTable: "OperationalNotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OperationalNotes_Users_TriageDecidedByUserId",
                table: "OperationalNotes",
                column: "TriageDecidedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationalNotes_OperationalNotes_DuplicateOfNoteId",
                table: "OperationalNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_OperationalNotes_Users_TriageDecidedByUserId",
                table: "OperationalNotes");

            migrationBuilder.DropTable(
                name: "NoteDecisionApprovals");

            migrationBuilder.DropTable(
                name: "NotePartsRequirements");

            migrationBuilder.DropTable(
                name: "NoteSlaPausePeriods");

            migrationBuilder.DropIndex(
                name: "IX_OperationalNotes_DuplicateOfNoteId",
                table: "OperationalNotes");

            migrationBuilder.DropIndex(
                name: "IX_OperationalNotes_TriageDecidedByUserId",
                table: "OperationalNotes");

            migrationBuilder.DropIndex(
                name: "IX_OperationalNotes_TriageOutcome",
                table: "OperationalNotes");

            migrationBuilder.DropColumn(
                name: "ClosureReason",
                table: "OperationalNotes");

            migrationBuilder.DropColumn(
                name: "DuplicateOfNoteId",
                table: "OperationalNotes");

            migrationBuilder.DropColumn(
                name: "NoActionJustificationAr",
                table: "OperationalNotes");

            migrationBuilder.DropColumn(
                name: "TreatmentExecutionType",
                table: "OperationalNotes");

            migrationBuilder.DropColumn(
                name: "TreatmentResultText",
                table: "OperationalNotes");

            migrationBuilder.DropColumn(
                name: "TreatmentResultType",
                table: "OperationalNotes");

            migrationBuilder.DropColumn(
                name: "TriageDecidedAtUtc",
                table: "OperationalNotes");

            migrationBuilder.DropColumn(
                name: "TriageDecidedByUserId",
                table: "OperationalNotes");

            migrationBuilder.DropColumn(
                name: "TriageOutcome",
                table: "OperationalNotes");

            migrationBuilder.DropColumn(
                name: "SupportsPartsWorkflow",
                table: "NoteTypes");
        }
    }
}
