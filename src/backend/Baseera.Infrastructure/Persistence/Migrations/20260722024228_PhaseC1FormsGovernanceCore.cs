using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PhaseC1FormsGovernanceCore : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE [FormDefinitions] (
                [Id] uniqueidentifier NOT NULL,
                [Code] nvarchar(80) NOT NULL,
                [NameAr] nvarchar(200) NOT NULL,
                [NameEn] nvarchar(200) NULL,
                [Description] nvarchar(2000) NOT NULL,
                [OwnerDepartmentId] uniqueidentifier NULL,
                [Classification] int NOT NULL,
                [ScopeType] int NOT NULL,
                [RegionId] uniqueidentifier NULL,
                [FacilityId] uniqueidentifier NULL,
                [FacilityUnitId] uniqueidentifier NULL,
                [Status] int NOT NULL,
                [CreatedByUserId] uniqueidentifier NOT NULL,
                [UpdatedByUserId] uniqueidentifier NULL,
                [SubmittedForReviewAtUtc] datetimeoffset NULL,
                [ApprovedAtUtc] datetimeoffset NULL,
                [ArchivedAtUtc] datetimeoffset NULL,
                [ArchivedByUserId] uniqueidentifier NULL,
                [DeletedByUserId] uniqueidentifier NULL,
                [LastModifiedByUserId] uniqueidentifier NULL,
                [CreatedAtUtc] datetimeoffset NOT NULL,
                [UpdatedAtUtc] datetimeoffset NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [RowVersion] rowversion NOT NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAtUtc] datetimeoffset NULL,
                [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_FormDefinitions] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_FormDefinitions_Facility_RequiresFacility]
                    CHECK (([ScopeType] <> 3) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)),
                CONSTRAINT [CK_FormDefinitions_GlobalHq_NoIds]
                    CHECK (([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)),
                CONSTRAINT [CK_FormDefinitions_Region_RequiresRegion]
                    CHECK (([ScopeType] <> 2) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)),
                CONSTRAINT [CK_FormDefinitions_Unit_RequiresUnit]
                    CHECK (([ScopeType] <> 4) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)),
                CONSTRAINT [FK_FormDefinitions_Departments_OwnerDepartmentId]
                    FOREIGN KEY ([OwnerDepartmentId]) REFERENCES [Departments] ([Id]),
                CONSTRAINT [FK_FormDefinitions_Facilities_FacilityId]
                    FOREIGN KEY ([FacilityId]) REFERENCES [Facilities] ([Id]),
                CONSTRAINT [FK_FormDefinitions_FacilityUnits_FacilityUnitId]
                    FOREIGN KEY ([FacilityUnitId]) REFERENCES [FacilityUnits] ([Id]),
                CONSTRAINT [FK_FormDefinitions_Regions_RegionId]
                    FOREIGN KEY ([RegionId]) REFERENCES [Regions] ([Id]),
                CONSTRAINT [FK_FormDefinitions_Users_ArchivedByUserId]
                    FOREIGN KEY ([ArchivedByUserId]) REFERENCES [Users] ([Id]),
                CONSTRAINT [FK_FormDefinitions_Users_CreatedByUserId]
                    FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]),
                CONSTRAINT [FK_FormDefinitions_Users_DeletedByUserId]
                    FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]),
                CONSTRAINT [FK_FormDefinitions_Users_LastModifiedByUserId]
                    FOREIGN KEY ([LastModifiedByUserId]) REFERENCES [Users] ([Id]),
                CONSTRAINT [FK_FormDefinitions_Users_UpdatedByUserId]
                    FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id])
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE [FormGovernancePolicies] (
                [Id] uniqueidentifier NOT NULL,
                [RequireReviewBeforeApproval] bit NOT NULL,
                [RequireSeparationOfDuties] bit NOT NULL,
                [AllowDesignerToReviewOwnForm] bit NOT NULL,
                [AllowReviewerToApproveOwnReview] bit NOT NULL,
                [AllowApproverToPublish] bit NOT NULL,
                [DefaultRetentionDays] int NOT NULL,
                [SensitiveRetentionDays] int NOT NULL,
                [MinimumRetentionDays] int NOT NULL,
                [AuditSensitiveViews] bit NOT NULL,
                [AuditExports] bit NOT NULL,
                [RequireReasonForArchive] bit NOT NULL,
                [UpdatedByUserId] uniqueidentifier NULL,
                [CreatedAtUtc] datetimeoffset NOT NULL,
                [UpdatedAtUtc] datetimeoffset NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [RowVersion] rowversion NOT NULL,
                CONSTRAINT [PK_FormGovernancePolicies] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_FormGovernancePolicies_DefaultRetentionDays_NonNegative]
                    CHECK ([DefaultRetentionDays] >= 0),
                CONSTRAINT [CK_FormGovernancePolicies_MinimumRetentionDays_NonNegative]
                    CHECK ([MinimumRetentionDays] >= 0),
                CONSTRAINT [CK_FormGovernancePolicies_SensitiveRetentionDays_NonNegative]
                    CHECK ([SensitiveRetentionDays] >= 0),
                CONSTRAINT [FK_FormGovernancePolicies_Users_UpdatedByUserId]
                    FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id])
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE [FormAccessGrants] (
                [Id] uniqueidentifier NOT NULL,
                [FormDefinitionId] uniqueidentifier NOT NULL,
                [PrincipalType] int NOT NULL,
                [PrincipalId] uniqueidentifier NOT NULL,
                [Capability] int NOT NULL,
                [Effect] int NOT NULL,
                [ScopeType] int NULL,
                [RegionId] uniqueidentifier NULL,
                [FacilityId] uniqueidentifier NULL,
                [ScopeKey] nvarchar(80) NOT NULL,
                [ValidFromUtc] datetimeoffset NULL,
                [ValidToUtc] datetimeoffset NULL,
                [Reason] nvarchar(1000) NOT NULL,
                [CreatedByUserId] uniqueidentifier NOT NULL,
                [RevokedByUserId] uniqueidentifier NULL,
                [RevokedAtUtc] datetimeoffset NULL,
                [CreatedAtUtc] datetimeoffset NOT NULL,
                [UpdatedAtUtc] datetimeoffset NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [RowVersion] rowversion NOT NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAtUtc] datetimeoffset NULL,
                [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_FormAccessGrants] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FormAccessGrants_Facilities_FacilityId]
                    FOREIGN KEY ([FacilityId]) REFERENCES [Facilities] ([Id]),
                CONSTRAINT [FK_FormAccessGrants_FormDefinitions_FormDefinitionId]
                    FOREIGN KEY ([FormDefinitionId]) REFERENCES [FormDefinitions] ([Id]),
                CONSTRAINT [FK_FormAccessGrants_Regions_RegionId]
                    FOREIGN KEY ([RegionId]) REFERENCES [Regions] ([Id]),
                CONSTRAINT [FK_FormAccessGrants_Users_CreatedByUserId]
                    FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]),
                CONSTRAINT [FK_FormAccessGrants_Users_RevokedByUserId]
                    FOREIGN KEY ([RevokedByUserId]) REFERENCES [Users] ([Id])
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE [FormReviewDecisions] (
                [Id] uniqueidentifier NOT NULL,
                [FormDefinitionId] uniqueidentifier NOT NULL,
                [Decision] int NOT NULL,
                [Reason] nvarchar(2000) NULL,
                [ReviewedByUserId] uniqueidentifier NOT NULL,
                [ReviewedAtUtc] datetimeoffset NOT NULL,
                [FromStatus] int NOT NULL,
                [ToStatus] int NOT NULL,
                [IsAdministrativeOverride] bit NOT NULL,
                CONSTRAINT [PK_FormReviewDecisions] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FormReviewDecisions_FormDefinitions_FormDefinitionId]
                    FOREIGN KEY ([FormDefinitionId]) REFERENCES [FormDefinitions] ([Id]),
                CONSTRAINT [FK_FormReviewDecisions_Users_ReviewedByUserId]
                    FOREIGN KEY ([ReviewedByUserId]) REFERENCES [Users] ([Id])
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX [IX_FormAccessGrants_CreatedByUserId]
                ON [FormAccessGrants] ([CreatedByUserId]);
            CREATE INDEX [IX_FormAccessGrants_FacilityId]
                ON [FormAccessGrants] ([FacilityId]);
            CREATE UNIQUE INDEX [IX_FormAccessGrants_FormDefinitionId_PrincipalType_PrincipalId_Capability_Effect_ScopeKey]
                ON [FormAccessGrants] ([FormDefinitionId], [PrincipalType], [PrincipalId], [Capability], [Effect], [ScopeKey])
                WHERE [IsDeleted] = 0;
            CREATE INDEX [IX_FormAccessGrants_RegionId]
                ON [FormAccessGrants] ([RegionId]);
            CREATE INDEX [IX_FormAccessGrants_RevokedByUserId]
                ON [FormAccessGrants] ([RevokedByUserId]);
            CREATE INDEX [IX_FormAccessGrants_ValidToUtc]
                ON [FormAccessGrants] ([ValidToUtc]);
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX [IX_FormDefinitions_ArchivedByUserId]
                ON [FormDefinitions] ([ArchivedByUserId]);
            CREATE INDEX [IX_FormDefinitions_Classification]
                ON [FormDefinitions] ([Classification]);
            CREATE UNIQUE INDEX [IX_FormDefinitions_Code]
                ON [FormDefinitions] ([Code])
                WHERE [IsDeleted] = 0;
            CREATE INDEX [IX_FormDefinitions_CreatedAtUtc]
                ON [FormDefinitions] ([CreatedAtUtc]);
            CREATE INDEX [IX_FormDefinitions_CreatedByUserId]
                ON [FormDefinitions] ([CreatedByUserId]);
            CREATE INDEX [IX_FormDefinitions_DeletedByUserId]
                ON [FormDefinitions] ([DeletedByUserId]);
            CREATE INDEX [IX_FormDefinitions_FacilityId]
                ON [FormDefinitions] ([FacilityId]);
            CREATE INDEX [IX_FormDefinitions_FacilityUnitId]
                ON [FormDefinitions] ([FacilityUnitId]);
            CREATE INDEX [IX_FormDefinitions_IsDeleted]
                ON [FormDefinitions] ([IsDeleted]);
            CREATE INDEX [IX_FormDefinitions_LastModifiedByUserId]
                ON [FormDefinitions] ([LastModifiedByUserId]);
            CREATE INDEX [IX_FormDefinitions_OwnerDepartmentId]
                ON [FormDefinitions] ([OwnerDepartmentId]);
            CREATE INDEX [IX_FormDefinitions_RegionId]
                ON [FormDefinitions] ([RegionId]);
            CREATE INDEX [IX_FormDefinitions_Status]
                ON [FormDefinitions] ([Status]);
            CREATE INDEX [IX_FormDefinitions_UpdatedByUserId]
                ON [FormDefinitions] ([UpdatedByUserId]);
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX [IX_FormGovernancePolicies_UpdatedByUserId]
                ON [FormGovernancePolicies] ([UpdatedByUserId]);
            CREATE INDEX [IX_FormReviewDecisions_FormDefinitionId]
                ON [FormReviewDecisions] ([FormDefinitionId]);
            CREATE INDEX [IX_FormReviewDecisions_ReviewedAtUtc]
                ON [FormReviewDecisions] ([ReviewedAtUtc]);
            CREATE INDEX [IX_FormReviewDecisions_ReviewedByUserId]
                ON [FormReviewDecisions] ([ReviewedByUserId]);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE [FormAccessGrants];");
        migrationBuilder.Sql("DROP TABLE [FormGovernancePolicies];");
        migrationBuilder.Sql("DROP TABLE [FormReviewDecisions];");
        migrationBuilder.Sql("DROP TABLE [FormDefinitions];");
    }
}
