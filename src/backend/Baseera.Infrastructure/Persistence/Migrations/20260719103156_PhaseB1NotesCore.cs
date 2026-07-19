using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseB1NotesCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "OperationalNoteReferenceSequence");

            migrationBuilder.CreateTable(
                name: "OperationalNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    ScopeType = table.Column<int>(type: "int", nullable: false),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WorkStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedForVerificationAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosureSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LastProcessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReopenedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReopenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_OperationalNotes", x => x.Id);
                    table.CheckConstraint("CK_OperationalNotes_Facility_RequiresFacility", "([ScopeType] <> 3) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)");
                    table.CheckConstraint("CK_OperationalNotes_GlobalHq_NoIds", "([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
                    table.CheckConstraint("CK_OperationalNotes_Region_RequiresRegion", "([ScopeType] <> 2) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
                    table.CheckConstraint("CK_OperationalNotes_SupportedScopes", "[ScopeType] IN (0, 1, 2, 3, 4)");
                    table.CheckConstraint("CK_OperationalNotes_Unit_RequiresFacilityAndUnit", "([ScopeType] <> 4) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_OperationalNotes_Departments_OwnerDepartmentId",
                        column: x => x.OwnerDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalNotes_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalNotes_FacilityUnits_FacilityUnitId",
                        column: x => x.FacilityUnitId,
                        principalTable: "FacilityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalNotes_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalNotes_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalNotes_Users_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalNotes_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_NoteAssignments", x => x.Id);
                    table.CheckConstraint("CK_NoteAssignments_UserXorDepartment", "([AssignedToUserId] IS NOT NULL AND [AssignedToDepartmentId] IS NULL) OR ([AssignedToUserId] IS NULL AND [AssignedToDepartmentId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_NoteAssignments_Departments_AssignedToDepartmentId",
                        column: x => x.AssignedToDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteAssignments_OperationalNotes_OperationalNoteId",
                        column: x => x.OperationalNoteId,
                        principalTable: "OperationalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteAssignments_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteAssignments_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_NoteStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteStatusHistory_NoteAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "NoteAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteStatusHistory_OperationalNotes_OperationalNoteId",
                        column: x => x.OperationalNoteId,
                        principalTable: "OperationalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteStatusHistory_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoteAssignments_AssignedByUserId",
                table: "NoteAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteAssignments_AssignedToDepartmentId",
                table: "NoteAssignments",
                column: "AssignedToDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteAssignments_AssignedToUserId",
                table: "NoteAssignments",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteAssignments_OperationalNoteId",
                table: "NoteAssignments",
                column: "OperationalNoteId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_NoteStatusHistory_AssignmentId",
                table: "NoteStatusHistory",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteStatusHistory_ChangedByUserId",
                table: "NoteStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteStatusHistory_OperationalNoteId_ChangedAtUtc",
                table: "NoteStatusHistory",
                columns: new[] { "OperationalNoteId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_ClosedByUserId",
                table: "OperationalNotes",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_CreatedAtUtc",
                table: "OperationalNotes",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_DueAtUtc",
                table: "OperationalNotes",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_FacilityId",
                table: "OperationalNotes",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_FacilityUnitId",
                table: "OperationalNotes",
                column: "FacilityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_OwnerDepartmentId",
                table: "OperationalNotes",
                column: "OwnerDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_ReferenceNumber",
                table: "OperationalNotes",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_RegionId",
                table: "OperationalNotes",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_ReopenedByUserId",
                table: "OperationalNotes",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_ReportedByUserId",
                table: "OperationalNotes",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_Severity",
                table: "OperationalNotes",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_Status",
                table: "OperationalNotes",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoteStatusHistory");

            migrationBuilder.DropTable(
                name: "NoteAssignments");

            migrationBuilder.DropTable(
                name: "OperationalNotes");

            migrationBuilder.DropSequence(
                name: "OperationalNoteReferenceSequence");
        }
    }
}
