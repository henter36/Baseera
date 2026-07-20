using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseB231NoteTypesAccessIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NoteTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EntryInstructionsAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DefaultSeverity = table.Column<int>(type: "int", nullable: false),
                    DefaultDueDays = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteTypes", x => x.Id);
                    table.CheckConstraint("CK_NoteTypes_DefaultDueDays_NonNegative", "[DefaultDueDays] IS NULL OR [DefaultDueDays] >= 0");
                    table.CheckConstraint("CK_NoteTypes_SortOrder_NonNegative", "[SortOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_NoteTypes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteTypes_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [NoteTypes] ([Id], [Code], [NameAr], [DescriptionAr], [EntryInstructionsAr], [SortOrder], [IsActive], [DefaultSeverity], [DefaultDueDays], [CreatedAtUtc])
                VALUES
                    ('44444444-4444-4444-4444-444444444401', N'SECURITY', N'أمنية', N'ملاحظات ذات صلة بالأمن والسلامة الأمنية.', N'حدد الموقع والأثر والإجراء المطلوب بوضوح.', 10, 1, 2, 7, SYSUTCDATETIME()),
                    ('44444444-4444-4444-4444-444444444402', N'TECHNICAL', N'فنية', N'ملاحظات الصيانة والأنظمة الفنية.', N'صف العطل أوالحالة الفنية والموقع المتأثر.', 20, 1, 1, 10, SYSUTCDATETIME()),
                    ('44444444-4444-4444-4444-444444444403', N'OPERATIONAL', N'تشغيلية', N'ملاحظات سير العمل والتشغيل اليومي.', N'وضح أثر الملاحظة على التشغيل والإجراء المتوقع.', 30, 1, 1, 10, SYSUTCDATETIME()),
                    ('44444444-4444-4444-4444-444444444404', N'HEALTH_SAFETY', N'صحة وسلامة', N'ملاحظات الصحة والسلامة المهنية.', N'اذكر الخطر والإجراءات الوقائية المطلوبة.', 40, 1, 2, 5, SYSUTCDATETIME()),
                    ('44444444-4444-4444-4444-444444444405', N'ADMINISTRATIVE', N'إدارية', N'ملاحظات إدارية وتنظيمية.', N'حدد الإجراء الإداري المطلوب والجهة المعنية.', 50, 1, 0, 14, SYSUTCDATETIME()),
                    ('44444444-4444-4444-4444-444444444406', N'OTHER', N'أخرى', N'ملاحظات لا تندرج ضمن الأنواع الأخرى.', N'اكتب تفاصيل كافية لتصنيف ومعالجة الملاحظة.', 60, 1, 1, 10, SYSUTCDATETIME());
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "NoteTypeId",
                table: "OperationalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [OperationalNotes]
                SET [NoteTypeId] = CASE [Category]
                    WHEN 0 THEN '44444444-4444-4444-4444-444444444401'
                    WHEN 1 THEN '44444444-4444-4444-4444-444444444402'
                    WHEN 2 THEN '44444444-4444-4444-4444-444444444403'
                    WHEN 3 THEN '44444444-4444-4444-4444-444444444404'
                    WHEN 4 THEN '44444444-4444-4444-4444-444444444405'
                    WHEN 5 THEN '44444444-4444-4444-4444-444444444406'
                    ELSE '44444444-4444-4444-4444-444444444406'
                END;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "NoteTypeId",
                table: "OperationalNotes",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Category",
                table: "OperationalNotes");

            migrationBuilder.CreateTable(
                name: "UserNoteIntakeProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockType = table.Column<int>(type: "int", nullable: false),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNoteIntakeProfiles", x => x.Id);
                    table.CheckConstraint("CK_UserNoteIntakeProfiles_Facility_RequiresFacility", "([LockType] <> 2) OR ([FacilityId] IS NOT NULL)");
                    table.CheckConstraint("CK_UserNoteIntakeProfiles_None_NoIds", "([LockType] <> 0) OR ([RegionId] IS NULL AND [FacilityId] IS NULL)");
                    table.CheckConstraint("CK_UserNoteIntakeProfiles_Region_RequiresRegion", "([LockType] <> 1) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_UserNoteIntakeProfiles_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserNoteIntakeProfiles_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserNoteIntakeProfiles_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserNoteIntakeProfiles_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserNoteIntakeProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleNoteTypeGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanView = table.Column<bool>(type: "bit", nullable: false),
                    CanCreate = table.Column<bool>(type: "bit", nullable: false),
                    CanAssign = table.Column<bool>(type: "bit", nullable: false),
                    CanProcess = table.Column<bool>(type: "bit", nullable: false),
                    CanSubmitForVerification = table.Column<bool>(type: "bit", nullable: false),
                    CanReview = table.Column<bool>(type: "bit", nullable: false),
                    CanCancel = table.Column<bool>(type: "bit", nullable: false),
                    CanReopen = table.Column<bool>(type: "bit", nullable: false),
                    CanArchive = table.Column<bool>(type: "bit", nullable: false),
                    CanRestore = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleNoteTypeGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleNoteTypeGrants_NoteTypes_NoteTypeId",
                        column: x => x.NoteTypeId,
                        principalTable: "NoteTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleNoteTypeGrants_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleNoteTypeGrants_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleNoteTypeGrants_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserNoteTypeOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanViewOverride = table.Column<bool>(type: "bit", nullable: true),
                    CanCreateOverride = table.Column<bool>(type: "bit", nullable: true),
                    CanAssignOverride = table.Column<bool>(type: "bit", nullable: true),
                    CanProcessOverride = table.Column<bool>(type: "bit", nullable: true),
                    CanSubmitForVerificationOverride = table.Column<bool>(type: "bit", nullable: true),
                    CanReviewOverride = table.Column<bool>(type: "bit", nullable: true),
                    CanCancelOverride = table.Column<bool>(type: "bit", nullable: true),
                    CanReopenOverride = table.Column<bool>(type: "bit", nullable: true),
                    CanArchiveOverride = table.Column<bool>(type: "bit", nullable: true),
                    CanRestoreOverride = table.Column<bool>(type: "bit", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNoteTypeOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNoteTypeOverrides_NoteTypes_NoteTypeId",
                        column: x => x.NoteTypeId,
                        principalTable: "NoteTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserNoteTypeOverrides_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserNoteTypeOverrides_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserNoteTypeOverrides_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_NoteTypeId",
                table: "OperationalNotes",
                column: "NoteTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteTypes_Code",
                table: "NoteTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoteTypes_CreatedByUserId",
                table: "NoteTypes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteTypes_SortOrder",
                table: "NoteTypes",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_NoteTypes_UpdatedByUserId",
                table: "NoteTypes",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleNoteTypeGrants_CreatedByUserId",
                table: "RoleNoteTypeGrants",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleNoteTypeGrants_NoteTypeId",
                table: "RoleNoteTypeGrants",
                column: "NoteTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleNoteTypeGrants_RoleId_NoteTypeId",
                table: "RoleNoteTypeGrants",
                columns: new[] { "RoleId", "NoteTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleNoteTypeGrants_UpdatedByUserId",
                table: "RoleNoteTypeGrants",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNoteIntakeProfiles_CreatedByUserId",
                table: "UserNoteIntakeProfiles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNoteIntakeProfiles_FacilityId",
                table: "UserNoteIntakeProfiles",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNoteIntakeProfiles_RegionId",
                table: "UserNoteIntakeProfiles",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNoteIntakeProfiles_UpdatedByUserId",
                table: "UserNoteIntakeProfiles",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNoteIntakeProfiles_UserId",
                table: "UserNoteIntakeProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNoteTypeOverrides_CreatedByUserId",
                table: "UserNoteTypeOverrides",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNoteTypeOverrides_NoteTypeId",
                table: "UserNoteTypeOverrides",
                column: "NoteTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNoteTypeOverrides_UpdatedByUserId",
                table: "UserNoteTypeOverrides",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNoteTypeOverrides_UserId_NoteTypeId",
                table: "UserNoteTypeOverrides",
                columns: new[] { "UserId", "NoteTypeId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OperationalNotes_NoteTypes_NoteTypeId",
                table: "OperationalNotes",
                column: "NoteTypeId",
                principalTable: "NoteTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationalNotes_NoteTypes_NoteTypeId",
                table: "OperationalNotes");

            migrationBuilder.DropTable(
                name: "RoleNoteTypeGrants");

            migrationBuilder.DropTable(
                name: "UserNoteIntakeProfiles");

            migrationBuilder.DropTable(
                name: "UserNoteTypeOverrides");

            migrationBuilder.DropIndex(
                name: "IX_OperationalNotes_NoteTypeId",
                table: "OperationalNotes");

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "OperationalNotes",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [OperationalNotes]
                SET [Category] = CASE [NoteTypeId]
                    WHEN '44444444-4444-4444-4444-444444444401' THEN 0
                    WHEN '44444444-4444-4444-4444-444444444402' THEN 1
                    WHEN '44444444-4444-4444-4444-444444444403' THEN 2
                    WHEN '44444444-4444-4444-4444-444444444404' THEN 3
                    WHEN '44444444-4444-4444-4444-444444444405' THEN 4
                    WHEN '44444444-4444-4444-4444-444444444406' THEN 5
                    ELSE 5
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Category",
                table: "OperationalNotes",
                type: "int",
                nullable: false,
                defaultValue: 5,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "NoteTypeId",
                table: "OperationalNotes");

            migrationBuilder.DropTable(
                name: "NoteTypes");
        }
    }
}
