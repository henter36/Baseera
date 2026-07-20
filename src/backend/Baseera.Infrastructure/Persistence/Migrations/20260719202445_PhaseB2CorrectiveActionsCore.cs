using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseB2CorrectiveActionsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "CorrectiveActionReferenceSequence");

            migrationBuilder.CreateTable(
                name: "CorrectiveActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OperationalNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    OwnerDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WorkStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedForVerificationAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletionSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReopenedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReopenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastProcessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_CorrectiveActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_Departments_OwnerDepartmentId",
                        column: x => x.OwnerDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_OperationalNotes_OperationalNoteId",
                        column: x => x.OperationalNoteId,
                        principalTable: "OperationalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_Users_LastProcessedByUserId",
                        column: x => x.LastProcessedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_Users_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveActionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectiveActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveActionAssignments", x => x.Id);
                    table.CheckConstraint("CK_CorrectiveActionAssignments_UserXorDepartment", "([AssignedToUserId] IS NOT NULL AND [AssignedToDepartmentId] IS NULL) OR ([AssignedToUserId] IS NULL AND [AssignedToDepartmentId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CorrectiveActionAssignments_CorrectiveActions_CorrectiveActionId",
                        column: x => x.CorrectiveActionId,
                        principalTable: "CorrectiveActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionAssignments_Departments_AssignedToDepartmentId",
                        column: x => x.AssignedToDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionAssignments_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionAssignments_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveActionStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectiveActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveActionStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionStatusHistory_CorrectiveActionAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "CorrectiveActionAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionStatusHistory_CorrectiveActions_CorrectiveActionId",
                        column: x => x.CorrectiveActionId,
                        principalTable: "CorrectiveActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionStatusHistory_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionAssignments_AssignedByUserId",
                table: "CorrectiveActionAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionAssignments_AssignedToDepartmentId",
                table: "CorrectiveActionAssignments",
                column: "AssignedToDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionAssignments_AssignedToUserId",
                table: "CorrectiveActionAssignments",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionAssignments_CorrectiveActionId",
                table: "CorrectiveActionAssignments",
                column: "CorrectiveActionId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_CancelledByUserId",
                table: "CorrectiveActions",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_CompletedByUserId",
                table: "CorrectiveActions",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_CreatedAtUtc",
                table: "CorrectiveActions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_CreatedByUserId",
                table: "CorrectiveActions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_DueAtUtc",
                table: "CorrectiveActions",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_IsDeleted",
                table: "CorrectiveActions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_LastProcessedByUserId",
                table: "CorrectiveActions",
                column: "LastProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_OperationalNoteId",
                table: "CorrectiveActions",
                column: "OperationalNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_OwnerDepartmentId",
                table: "CorrectiveActions",
                column: "OwnerDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_Priority",
                table: "CorrectiveActions",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_ReferenceNumber",
                table: "CorrectiveActions",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_ReopenedByUserId",
                table: "CorrectiveActions",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_Status",
                table: "CorrectiveActions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_Status_DueAtUtc",
                table: "CorrectiveActions",
                columns: new[] { "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionStatusHistory_AssignmentId",
                table: "CorrectiveActionStatusHistory",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionStatusHistory_ChangedByUserId",
                table: "CorrectiveActionStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionStatusHistory_CorrectiveActionId_ChangedAtUtc",
                table: "CorrectiveActionStatusHistory",
                columns: new[] { "CorrectiveActionId", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrectiveActionStatusHistory");

            migrationBuilder.DropTable(
                name: "CorrectiveActionAssignments");

            migrationBuilder.DropTable(
                name: "CorrectiveActions");

            migrationBuilder.DropSequence(
                name: "CorrectiveActionReferenceSequence");
        }
    }
}
