using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseD4OccupancyInmateMovement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FacilityCapacityBaselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CapacityType = table.Column<int>(type: "int", nullable: false),
                    ApprovedCapacity = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovalReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ApprovalDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_FacilityCapacityBaselines", x => x.Id);
                    table.CheckConstraint("CK_FacilityCapacityBaselines_Capacity_Positive", "[ApprovedCapacity] > 0");
                    table.CheckConstraint("CK_FacilityCapacityBaselines_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.ForeignKey(
                        name: "FK_FacilityCapacityBaselines_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacilityCapacityBaselines_FacilityUnits_FacilityUnitId",
                        column: x => x.FacilityUnitId,
                        principalTable: "FacilityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacilityCapacityBaselines_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InmateCensusSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InmateCount = table.Column<int>(type: "int", nullable: false),
                    MaleCount = table.Column<int>(type: "int", nullable: true),
                    FemaleCount = table.Column<int>(type: "int", nullable: true),
                    AdultCount = table.Column<int>(type: "int", nullable: true),
                    JuvenileCount = table.Column<int>(type: "int", nullable: true),
                    MedicalCount = table.Column<int>(type: "int", nullable: true),
                    IsolationCount = table.Column<int>(type: "int", nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SourceVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsAuthoritative = table.Column<bool>(type: "bit", nullable: false),
                    QualityStatus = table.Column<int>(type: "int", nullable: false),
                    QualityNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_InmateCensusSnapshots", x => x.Id);
                    table.CheckConstraint("CK_InmateCensusSnapshots_Adult_NonNegative", "[AdultCount] IS NULL OR [AdultCount] >= 0");
                    table.CheckConstraint("CK_InmateCensusSnapshots_Count_NonNegative", "[InmateCount] >= 0");
                    table.CheckConstraint("CK_InmateCensusSnapshots_Female_NonNegative", "[FemaleCount] IS NULL OR [FemaleCount] >= 0");
                    table.CheckConstraint("CK_InmateCensusSnapshots_Isolation_NonNegative", "[IsolationCount] IS NULL OR [IsolationCount] >= 0");
                    table.CheckConstraint("CK_InmateCensusSnapshots_Juvenile_NonNegative", "[JuvenileCount] IS NULL OR [JuvenileCount] >= 0");
                    table.CheckConstraint("CK_InmateCensusSnapshots_Male_NonNegative", "[MaleCount] IS NULL OR [MaleCount] >= 0");
                    table.CheckConstraint("CK_InmateCensusSnapshots_Medical_NonNegative", "[MedicalCount] IS NULL OR [MedicalCount] >= 0");
                    table.ForeignKey(
                        name: "FK_InmateCensusSnapshots_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InmateCensusSnapshots_FacilityUnits_FacilityUnitId",
                        column: x => x.FacilityUnitId,
                        principalTable: "FacilityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InmateCensusSnapshots_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InmateMovementEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InmateReferenceHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    FromFacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToFacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromFacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToFacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ExternalEventId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversedByEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_InmateMovementEvents", x => x.Id);
                    table.CheckConstraint("CK_InmateMovementEvents_Admission_Target", "([MovementType] <> 0) OR ([ToFacilityId] IS NOT NULL)");
                    table.CheckConstraint("CK_InmateMovementEvents_InternalTransfer_Units", "([MovementType] <> 4) OR ([FromFacilityUnitId] IS NOT NULL AND [ToFacilityUnitId] IS NOT NULL)");
                    table.CheckConstraint("CK_InmateMovementEvents_NoSelfTransfer", "([FromFacilityId] IS NULL OR [ToFacilityId] IS NULL OR [FromFacilityId] <> [ToFacilityId] OR ISNULL([FromFacilityUnitId], '00000000-0000-0000-0000-000000000000') <> ISNULL([ToFacilityUnitId], '00000000-0000-0000-0000-000000000000'))");
                    table.CheckConstraint("CK_InmateMovementEvents_Release_Source", "([MovementType] <> 1) OR ([FromFacilityId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_InmateMovementEvents_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InmateMovementEvents_Facilities_FromFacilityId",
                        column: x => x.FromFacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InmateMovementEvents_Facilities_ToFacilityId",
                        column: x => x.ToFacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InmateMovementEvents_FacilityUnits_FromFacilityUnitId",
                        column: x => x.FromFacilityUnitId,
                        principalTable: "FacilityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InmateMovementEvents_FacilityUnits_ToFacilityUnitId",
                        column: x => x.ToFacilityUnitId,
                        principalTable: "FacilityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InmateMovementEvents_InmateMovementEvents_ReversedByEventId",
                        column: x => x.ReversedByEventId,
                        principalTable: "InmateMovementEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InmateMovementEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityCapacityBaselines_FacilityId_FacilityUnitId_CapacityType_EffectiveFromUtc",
                table: "FacilityCapacityBaselines",
                columns: new[] { "FacilityId", "FacilityUnitId", "CapacityType", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityCapacityBaselines_FacilityId_IsDeleted",
                table: "FacilityCapacityBaselines",
                columns: new[] { "FacilityId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityCapacityBaselines_FacilityUnitId",
                table: "FacilityCapacityBaselines",
                column: "FacilityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityCapacityBaselines_OrganizationId",
                table: "FacilityCapacityBaselines",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_InmateCensusSnapshots_FacilityId_FacilityUnitId_CapturedAtUtc",
                table: "InmateCensusSnapshots",
                columns: new[] { "FacilityId", "FacilityUnitId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InmateCensusSnapshots_FacilityId_IsAuthoritative_CapturedAtUtc",
                table: "InmateCensusSnapshots",
                columns: new[] { "FacilityId", "IsAuthoritative", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InmateCensusSnapshots_FacilityId_IsDeleted",
                table: "InmateCensusSnapshots",
                columns: new[] { "FacilityId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_InmateCensusSnapshots_FacilityUnitId",
                table: "InmateCensusSnapshots",
                column: "FacilityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InmateCensusSnapshots_OrganizationId",
                table: "InmateCensusSnapshots",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_InmateMovementEvents_FacilityId_OccurredAtUtc_MovementType",
                table: "InmateMovementEvents",
                columns: new[] { "FacilityId", "OccurredAtUtc", "MovementType" });

            migrationBuilder.CreateIndex(
                name: "IX_InmateMovementEvents_FromFacilityId",
                table: "InmateMovementEvents",
                column: "FromFacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_InmateMovementEvents_FromFacilityUnitId_OccurredAtUtc",
                table: "InmateMovementEvents",
                columns: new[] { "FromFacilityUnitId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InmateMovementEvents_OrganizationId",
                table: "InmateMovementEvents",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_InmateMovementEvents_ReversedByEventId",
                table: "InmateMovementEvents",
                column: "ReversedByEventId");

            migrationBuilder.CreateIndex(
                name: "IX_InmateMovementEvents_SourceType_SourceReference_ExternalEventId",
                table: "InmateMovementEvents",
                columns: new[] { "SourceType", "SourceReference", "ExternalEventId" },
                unique: true,
                filter: "[ExternalEventId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InmateMovementEvents_ToFacilityId",
                table: "InmateMovementEvents",
                column: "ToFacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_InmateMovementEvents_ToFacilityUnitId_OccurredAtUtc",
                table: "InmateMovementEvents",
                columns: new[] { "ToFacilityUnitId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacilityCapacityBaselines");

            migrationBuilder.DropTable(
                name: "InmateCensusSnapshots");

            migrationBuilder.DropTable(
                name: "InmateMovementEvents");
        }
    }
}
