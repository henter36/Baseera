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
            migrationBuilder.Sql(
                """
                CREATE TABLE [NoteTypes] (
                    [Id] uniqueidentifier NOT NULL,
                    [Code] nvarchar(50) NOT NULL,
                    [NameAr] nvarchar(200) NOT NULL,
                    [DescriptionAr] nvarchar(1000) NULL,
                    [EntryInstructionsAr] nvarchar(2000) NULL,
                    [SortOrder] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    [DefaultSeverity] int NOT NULL,
                    [DefaultDueDays] int NULL,
                    [CreatedByUserId] uniqueidentifier NULL,
                    [UpdatedByUserId] uniqueidentifier NULL,
                    [CreatedAtUtc] datetimeoffset NOT NULL,
                    [UpdatedAtUtc] datetimeoffset NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    [UpdatedBy] nvarchar(max) NULL,
                    [RowVersion] rowversion NOT NULL,
                    CONSTRAINT [PK_NoteTypes] PRIMARY KEY ([Id]),
                    CONSTRAINT [CK_NoteTypes_DefaultDueDays_NonNegative] CHECK ([DefaultDueDays] IS NULL OR [DefaultDueDays] >= 0),
                    CONSTRAINT [CK_NoteTypes_SortOrder_NonNegative] CHECK ([SortOrder] >= 0),
                    CONSTRAINT [FK_NoteTypes_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_NoteTypes_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                );
                """);

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

            migrationBuilder.Sql(
                """
                CREATE TABLE [UserNoteIntakeProfiles] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [LockType] int NOT NULL,
                    [RegionId] uniqueidentifier NULL,
                    [FacilityId] uniqueidentifier NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedByUserId] uniqueidentifier NULL,
                    [UpdatedByUserId] uniqueidentifier NULL,
                    [CreatedAtUtc] datetimeoffset NOT NULL,
                    [UpdatedAtUtc] datetimeoffset NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    [UpdatedBy] nvarchar(max) NULL,
                    [RowVersion] rowversion NOT NULL,
                    CONSTRAINT [PK_UserNoteIntakeProfiles] PRIMARY KEY ([Id]),
                    CONSTRAINT [CK_UserNoteIntakeProfiles_Facility_RequiresFacility] CHECK (([LockType] <> 2) OR ([FacilityId] IS NOT NULL)),
                    CONSTRAINT [CK_UserNoteIntakeProfiles_None_NoIds] CHECK (([LockType] <> 0) OR ([RegionId] IS NULL AND [FacilityId] IS NULL)),
                    CONSTRAINT [CK_UserNoteIntakeProfiles_Region_RequiresRegion] CHECK (([LockType] <> 1) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL)),
                    CONSTRAINT [FK_UserNoteIntakeProfiles_Facilities_FacilityId] FOREIGN KEY ([FacilityId]) REFERENCES [Facilities] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_UserNoteIntakeProfiles_Regions_RegionId] FOREIGN KEY ([RegionId]) REFERENCES [Regions] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_UserNoteIntakeProfiles_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_UserNoteIntakeProfiles_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_UserNoteIntakeProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                );

                CREATE TABLE [RoleNoteTypeGrants] (
                    [Id] uniqueidentifier NOT NULL,
                    [RoleId] uniqueidentifier NOT NULL,
                    [NoteTypeId] uniqueidentifier NOT NULL,
                    [CanView] bit NOT NULL,
                    [CanCreate] bit NOT NULL,
                    [CanAssign] bit NOT NULL,
                    [CanProcess] bit NOT NULL,
                    [CanSubmitForVerification] bit NOT NULL,
                    [CanReview] bit NOT NULL,
                    [CanCancel] bit NOT NULL,
                    [CanReopen] bit NOT NULL,
                    [CanArchive] bit NOT NULL,
                    [CanRestore] bit NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedByUserId] uniqueidentifier NULL,
                    [UpdatedByUserId] uniqueidentifier NULL,
                    [CreatedAtUtc] datetimeoffset NOT NULL,
                    [UpdatedAtUtc] datetimeoffset NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    [UpdatedBy] nvarchar(max) NULL,
                    [RowVersion] rowversion NOT NULL,
                    CONSTRAINT [PK_RoleNoteTypeGrants] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_RoleNoteTypeGrants_NoteTypes_NoteTypeId] FOREIGN KEY ([NoteTypeId]) REFERENCES [NoteTypes] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_RoleNoteTypeGrants_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_RoleNoteTypeGrants_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_RoleNoteTypeGrants_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                );

                CREATE TABLE [UserNoteTypeOverrides] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [NoteTypeId] uniqueidentifier NOT NULL,
                    [CanViewOverride] bit NULL,
                    [CanCreateOverride] bit NULL,
                    [CanAssignOverride] bit NULL,
                    [CanProcessOverride] bit NULL,
                    [CanSubmitForVerificationOverride] bit NULL,
                    [CanReviewOverride] bit NULL,
                    [CanCancelOverride] bit NULL,
                    [CanReopenOverride] bit NULL,
                    [CanArchiveOverride] bit NULL,
                    [CanRestoreOverride] bit NULL,
                    [IsActive] bit NOT NULL,
                    [Reason] nvarchar(1000) NOT NULL,
                    [CreatedByUserId] uniqueidentifier NULL,
                    [UpdatedByUserId] uniqueidentifier NULL,
                    [CreatedAtUtc] datetimeoffset NOT NULL,
                    [UpdatedAtUtc] datetimeoffset NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    [UpdatedBy] nvarchar(max) NULL,
                    [RowVersion] rowversion NOT NULL,
                    CONSTRAINT [PK_UserNoteTypeOverrides] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_UserNoteTypeOverrides_NoteTypes_NoteTypeId] FOREIGN KEY ([NoteTypeId]) REFERENCES [NoteTypes] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_UserNoteTypeOverrides_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_UserNoteTypeOverrides_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_UserNoteTypeOverrides_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX [IX_NoteTypes_Code] ON [NoteTypes] ([Code]);
                CREATE INDEX [IX_NoteTypes_CreatedByUserId] ON [NoteTypes] ([CreatedByUserId]);
                CREATE INDEX [IX_NoteTypes_SortOrder] ON [NoteTypes] ([SortOrder]);
                CREATE INDEX [IX_NoteTypes_UpdatedByUserId] ON [NoteTypes] ([UpdatedByUserId]);
                CREATE INDEX [IX_RoleNoteTypeGrants_CreatedByUserId] ON [RoleNoteTypeGrants] ([CreatedByUserId]);
                CREATE INDEX [IX_RoleNoteTypeGrants_NoteTypeId] ON [RoleNoteTypeGrants] ([NoteTypeId]);
                CREATE UNIQUE INDEX [IX_RoleNoteTypeGrants_RoleId_NoteTypeId] ON [RoleNoteTypeGrants] ([RoleId], [NoteTypeId]);
                CREATE INDEX [IX_RoleNoteTypeGrants_UpdatedByUserId] ON [RoleNoteTypeGrants] ([UpdatedByUserId]);
                CREATE INDEX [IX_UserNoteIntakeProfiles_CreatedByUserId] ON [UserNoteIntakeProfiles] ([CreatedByUserId]);
                CREATE INDEX [IX_UserNoteIntakeProfiles_FacilityId] ON [UserNoteIntakeProfiles] ([FacilityId]);
                CREATE INDEX [IX_UserNoteIntakeProfiles_RegionId] ON [UserNoteIntakeProfiles] ([RegionId]);
                CREATE INDEX [IX_UserNoteIntakeProfiles_UpdatedByUserId] ON [UserNoteIntakeProfiles] ([UpdatedByUserId]);
                CREATE UNIQUE INDEX [IX_UserNoteIntakeProfiles_UserId] ON [UserNoteIntakeProfiles] ([UserId]);
                CREATE INDEX [IX_UserNoteTypeOverrides_CreatedByUserId] ON [UserNoteTypeOverrides] ([CreatedByUserId]);
                CREATE INDEX [IX_UserNoteTypeOverrides_NoteTypeId] ON [UserNoteTypeOverrides] ([NoteTypeId]);
                CREATE INDEX [IX_UserNoteTypeOverrides_UpdatedByUserId] ON [UserNoteTypeOverrides] ([UpdatedByUserId]);
                CREATE UNIQUE INDEX [IX_UserNoteTypeOverrides_UserId_NoteTypeId] ON [UserNoteTypeOverrides] ([UserId], [NoteTypeId]);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotes_NoteTypeId",
                table: "OperationalNotes",
                column: "NoteTypeId");

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
