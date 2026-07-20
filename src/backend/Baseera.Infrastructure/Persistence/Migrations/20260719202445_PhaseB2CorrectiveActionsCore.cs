using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PhaseB2CorrectiveActionsCore : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE SEQUENCE [CorrectiveActionReferenceSequence] AS bigint
                START WITH 1
                INCREMENT BY 1;
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE [CorrectiveActions] (
                [Id] uniqueidentifier NOT NULL,
                [ReferenceNumber] nvarchar(20) NOT NULL,
                [OperationalNoteId] uniqueidentifier NOT NULL,
                [Title] nvarchar(300) NOT NULL,
                [Description] nvarchar(max) NOT NULL,
                [Priority] int NOT NULL,
                [Status] int NOT NULL,
                [Classification] int NOT NULL,
                [OwnerDepartmentId] uniqueidentifier NULL,
                [CreatedByUserId] uniqueidentifier NOT NULL,
                [SubmittedAtUtc] datetimeoffset NULL,
                [WorkStartedAtUtc] datetimeoffset NULL,
                [SubmittedForVerificationAtUtc] datetimeoffset NULL,
                [CompletedAtUtc] datetimeoffset NULL,
                [CompletedByUserId] uniqueidentifier NULL,
                [CompletionSummary] nvarchar(2000) NULL,
                [ReopenedAtUtc] datetimeoffset NULL,
                [ReopenedByUserId] uniqueidentifier NULL,
                [ReopenReason] nvarchar(2000) NULL,
                [CancelledAtUtc] datetimeoffset NULL,
                [CancelledByUserId] uniqueidentifier NULL,
                [CancelReason] nvarchar(2000) NULL,
                [DueAtUtc] datetimeoffset NULL,
                [LastProcessedByUserId] uniqueidentifier NULL,
                [CreatedAtUtc] datetimeoffset NOT NULL,
                [UpdatedAtUtc] datetimeoffset NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [RowVersion] rowversion NOT NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAtUtc] datetimeoffset NULL,
                [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_CorrectiveActions] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_CorrectiveActions_Departments_OwnerDepartmentId]
                    FOREIGN KEY ([OwnerDepartmentId]) REFERENCES [Departments] ([Id]),
                CONSTRAINT [FK_CorrectiveActions_OperationalNotes_OperationalNoteId]
                    FOREIGN KEY ([OperationalNoteId]) REFERENCES [OperationalNotes] ([Id]),
                CONSTRAINT [FK_CorrectiveActions_Users_CancelledByUserId]
                    FOREIGN KEY ([CancelledByUserId]) REFERENCES [Users] ([Id]),
                CONSTRAINT [FK_CorrectiveActions_Users_CompletedByUserId]
                    FOREIGN KEY ([CompletedByUserId]) REFERENCES [Users] ([Id]),
                CONSTRAINT [FK_CorrectiveActions_Users_CreatedByUserId]
                    FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]),
                CONSTRAINT [FK_CorrectiveActions_Users_LastProcessedByUserId]
                    FOREIGN KEY ([LastProcessedByUserId]) REFERENCES [Users] ([Id]),
                CONSTRAINT [FK_CorrectiveActions_Users_ReopenedByUserId]
                    FOREIGN KEY ([ReopenedByUserId]) REFERENCES [Users] ([Id])
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE [CorrectiveActionAssignments] (
                [Id] uniqueidentifier NOT NULL,
                [CorrectiveActionId] uniqueidentifier NOT NULL,
                [AssignedToUserId] uniqueidentifier NULL,
                [AssignedToDepartmentId] uniqueidentifier NULL,
                [AssignedByUserId] uniqueidentifier NOT NULL,
                [AssignedAtUtc] datetimeoffset NOT NULL,
                [DueAtUtc] datetimeoffset NULL,
                [Reason] nvarchar(2000) NOT NULL,
                [AcceptedAtUtc] datetimeoffset NULL,
                [CompletedAtUtc] datetimeoffset NULL,
                [EndedAtUtc] datetimeoffset NULL,
                [EndReason] nvarchar(2000) NULL,
                [IsCurrent] bit NOT NULL,
                [CreatedAtUtc] datetimeoffset NOT NULL,
                [UpdatedAtUtc] datetimeoffset NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [RowVersion] rowversion NOT NULL,
                CONSTRAINT [PK_CorrectiveActionAssignments] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_CorrectiveActionAssignments_UserXorDepartment]
                    CHECK (([AssignedToUserId] IS NOT NULL AND [AssignedToDepartmentId] IS NULL) OR ([AssignedToUserId] IS NULL AND [AssignedToDepartmentId] IS NOT NULL)),
                CONSTRAINT [FK_CorrectiveActionAssignments_CorrectiveActions_CorrectiveActionId]
                    FOREIGN KEY ([CorrectiveActionId]) REFERENCES [CorrectiveActions] ([Id]),
                CONSTRAINT [FK_CorrectiveActionAssignments_Departments_AssignedToDepartmentId]
                    FOREIGN KEY ([AssignedToDepartmentId]) REFERENCES [Departments] ([Id]),
                CONSTRAINT [FK_CorrectiveActionAssignments_Users_AssignedByUserId]
                    FOREIGN KEY ([AssignedByUserId]) REFERENCES [Users] ([Id]),
                CONSTRAINT [FK_CorrectiveActionAssignments_Users_AssignedToUserId]
                    FOREIGN KEY ([AssignedToUserId]) REFERENCES [Users] ([Id])
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE [CorrectiveActionStatusHistory] (
                [Id] uniqueidentifier NOT NULL,
                [CorrectiveActionId] uniqueidentifier NOT NULL,
                [FromStatus] int NULL,
                [ToStatus] int NOT NULL,
                [ChangedByUserId] uniqueidentifier NOT NULL,
                [ChangedAtUtc] datetimeoffset NOT NULL,
                [Reason] nvarchar(2000) NULL,
                [AssignmentId] uniqueidentifier NULL,
                [MetadataJson] nvarchar(4000) NULL,
                CONSTRAINT [PK_CorrectiveActionStatusHistory] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_CorrectiveActionStatusHistory_CorrectiveActionAssignments_AssignmentId]
                    FOREIGN KEY ([AssignmentId]) REFERENCES [CorrectiveActionAssignments] ([Id]),
                CONSTRAINT [FK_CorrectiveActionStatusHistory_CorrectiveActions_CorrectiveActionId]
                    FOREIGN KEY ([CorrectiveActionId]) REFERENCES [CorrectiveActions] ([Id]),
                CONSTRAINT [FK_CorrectiveActionStatusHistory_Users_ChangedByUserId]
                    FOREIGN KEY ([ChangedByUserId]) REFERENCES [Users] ([Id])
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX [IX_CorrectiveActionAssignments_AssignedByUserId]
                ON [CorrectiveActionAssignments] ([AssignedByUserId]);
            CREATE INDEX [IX_CorrectiveActionAssignments_AssignedToDepartmentId]
                ON [CorrectiveActionAssignments] ([AssignedToDepartmentId]);
            CREATE INDEX [IX_CorrectiveActionAssignments_AssignedToUserId]
                ON [CorrectiveActionAssignments] ([AssignedToUserId]);
            CREATE UNIQUE INDEX [IX_CorrectiveActionAssignments_CorrectiveActionId]
                ON [CorrectiveActionAssignments] ([CorrectiveActionId])
                WHERE [IsCurrent] = 1;
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX [IX_CorrectiveActions_CancelledByUserId] ON [CorrectiveActions] ([CancelledByUserId]);
            CREATE INDEX [IX_CorrectiveActions_CompletedByUserId] ON [CorrectiveActions] ([CompletedByUserId]);
            CREATE INDEX [IX_CorrectiveActions_CreatedAtUtc] ON [CorrectiveActions] ([CreatedAtUtc]);
            CREATE INDEX [IX_CorrectiveActions_CreatedByUserId] ON [CorrectiveActions] ([CreatedByUserId]);
            CREATE INDEX [IX_CorrectiveActions_DueAtUtc] ON [CorrectiveActions] ([DueAtUtc]);
            CREATE INDEX [IX_CorrectiveActions_IsDeleted] ON [CorrectiveActions] ([IsDeleted]);
            CREATE INDEX [IX_CorrectiveActions_LastProcessedByUserId] ON [CorrectiveActions] ([LastProcessedByUserId]);
            CREATE INDEX [IX_CorrectiveActions_OperationalNoteId] ON [CorrectiveActions] ([OperationalNoteId]);
            CREATE INDEX [IX_CorrectiveActions_OwnerDepartmentId] ON [CorrectiveActions] ([OwnerDepartmentId]);
            CREATE INDEX [IX_CorrectiveActions_Priority] ON [CorrectiveActions] ([Priority]);
            CREATE UNIQUE INDEX [IX_CorrectiveActions_ReferenceNumber] ON [CorrectiveActions] ([ReferenceNumber]);
            CREATE INDEX [IX_CorrectiveActions_ReopenedByUserId] ON [CorrectiveActions] ([ReopenedByUserId]);
            CREATE INDEX [IX_CorrectiveActions_Status] ON [CorrectiveActions] ([Status]);
            CREATE INDEX [IX_CorrectiveActions_Status_DueAtUtc] ON [CorrectiveActions] ([Status], [DueAtUtc]);
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX [IX_CorrectiveActionStatusHistory_AssignmentId]
                ON [CorrectiveActionStatusHistory] ([AssignmentId]);
            CREATE INDEX [IX_CorrectiveActionStatusHistory_ChangedByUserId]
                ON [CorrectiveActionStatusHistory] ([ChangedByUserId]);
            CREATE INDEX [IX_CorrectiveActionStatusHistory_CorrectiveActionId_ChangedAtUtc]
                ON [CorrectiveActionStatusHistory] ([CorrectiveActionId], [ChangedAtUtc]);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE [CorrectiveActionStatusHistory];");
        migrationBuilder.Sql("DROP TABLE [CorrectiveActionAssignments];");
        migrationBuilder.Sql("DROP TABLE [CorrectiveActions];");
        migrationBuilder.Sql("DROP SEQUENCE [CorrectiveActionReferenceSequence];");
    }
}
