using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseB22EscalationsNotificationsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackgroundJobLeases",
                columns: table => new
                {
                    JobName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LeaseOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LeaseAcquiredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HeartbeatAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobLeases", x => x.JobName);
                });

            migrationBuilder.CreateTable(
                name: "EscalationPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ScopeType = table.Column<int>(type: "int", nullable: false),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActivatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeactivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeactivatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_EscalationPolicies", x => x.Id);
                    table.CheckConstraint("CK_EscalationPolicies_Facility_RequiresFacility", "([ScopeType] <> 3) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)");
                    table.CheckConstraint("CK_EscalationPolicies_GlobalHq_NoIds", "([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
                    table.CheckConstraint("CK_EscalationPolicies_Region_RequiresRegion", "([ScopeType] <> 2) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
                    table.CheckConstraint("CK_EscalationPolicies_Unit_RequiresFacilityAndUnit", "([ScopeType] <> 4) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_EscalationPolicies_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscalationPolicies_FacilityUnits_FacilityUnitId",
                        column: x => x.FacilityUnitId,
                        principalTable: "FacilityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscalationPolicies_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscalationPolicies_Users_ActivatedByUserId",
                        column: x => x.ActivatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscalationPolicies_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscalationPolicies_Users_DeactivatedByUserId",
                        column: x => x.DeactivatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EscalationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EscalationPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    ThresholdDays = table.Column<int>(type: "int", nullable: false),
                    RepeatEveryDays = table.Column<int>(type: "int", nullable: true),
                    MaximumOccurrences = table.Column<int>(type: "int", nullable: true),
                    RecipientStrategy = table.Column<int>(type: "int", nullable: false),
                    RecipientRoleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SpecificRecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TitleTemplateAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MessageTemplateAr = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_EscalationRules", x => x.Id);
                    table.CheckConstraint("CK_EscalationRules_Level_Positive", "[Level] > 0");
                    table.CheckConstraint("CK_EscalationRules_Max_Positive_WhenPresent", "[MaximumOccurrences] IS NULL OR [MaximumOccurrences] > 0");
                    table.CheckConstraint("CK_EscalationRules_Repeat_Positive_WhenPresent", "[RepeatEveryDays] IS NULL OR [RepeatEveryDays] > 0");
                    table.CheckConstraint("CK_EscalationRules_RoleStrategy_RoleRequired", "([RecipientStrategy] <> 2) OR ([RecipientRoleCode] IS NOT NULL)");
                    table.CheckConstraint("CK_EscalationRules_Threshold_NonNegative", "[ThresholdDays] >= 0");
                    table.CheckConstraint("CK_EscalationRules_UserStrategy_UserRequired", "([RecipientStrategy] <> 1) OR ([SpecificRecipientUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_EscalationRules_EscalationPolicies_EscalationPolicyId",
                        column: x => x.EscalationPolicyId,
                        principalTable: "EscalationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscalationRules_Users_SpecificRecipientUserId",
                        column: x => x.SpecificRecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EscalationOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    OccurrenceNumber = table.Column<int>(type: "int", nullable: false),
                    OccurrenceKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DetectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecipientCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SuppressionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalationOccurrences_EscalationPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "EscalationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscalationOccurrences_EscalationRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "EscalationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EscalationOccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MessageAr = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_EscalationOccurrences_EscalationOccurrenceId",
                        column: x => x.EscalationOccurrenceId,
                        principalTable: "EscalationOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextRetryAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessageSafe = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveryAttempts_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobLeases_LeaseExpiresAtUtc",
                table: "BackgroundJobLeases",
                column: "LeaseExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationOccurrences_OccurrenceKey",
                table: "EscalationOccurrences",
                column: "OccurrenceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalationOccurrences_PolicyId",
                table: "EscalationOccurrences",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationOccurrences_RuleId",
                table: "EscalationOccurrences",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationOccurrences_Status",
                table: "EscalationOccurrences",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationOccurrences_TargetType_DueAtUtc",
                table: "EscalationOccurrences",
                columns: new[] { "TargetType", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EscalationOccurrences_TargetType_TargetId",
                table: "EscalationOccurrences",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_EscalationPolicies_ActivatedByUserId",
                table: "EscalationPolicies",
                column: "ActivatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationPolicies_Code",
                table: "EscalationPolicies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalationPolicies_CreatedByUserId",
                table: "EscalationPolicies",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationPolicies_DeactivatedByUserId",
                table: "EscalationPolicies",
                column: "DeactivatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationPolicies_FacilityId",
                table: "EscalationPolicies",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationPolicies_FacilityUnitId",
                table: "EscalationPolicies",
                column: "FacilityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationPolicies_IsDeleted",
                table: "EscalationPolicies",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationPolicies_IsEnabled_TargetType_ScopeType",
                table: "EscalationPolicies",
                columns: new[] { "IsEnabled", "TargetType", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "IX_EscalationPolicies_RegionId",
                table: "EscalationPolicies",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_EscalationPolicyId_Level",
                table: "EscalationRules",
                columns: new[] { "EscalationPolicyId", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_IsDeleted",
                table: "EscalationRules",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_IsEnabled_TriggerType_ThresholdDays",
                table: "EscalationRules",
                columns: new[] { "IsEnabled", "TriggerType", "ThresholdDays" });

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_SpecificRecipientUserId",
                table: "EscalationRules",
                column: "SpecificRecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_NotificationId_Channel_AttemptNumber",
                table: "NotificationDeliveryAttempts",
                columns: new[] { "NotificationId", "Channel", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_Status_NextRetryAtUtc",
                table: "NotificationDeliveryAttempts",
                columns: new[] { "Status", "NextRetryAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DeduplicationKey",
                table: "Notifications",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EscalationOccurrenceId",
                table: "Notifications",
                column: "EscalationOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_Status_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TargetType_TargetId",
                table: "Notifications",
                columns: new[] { "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackgroundJobLeases");

            migrationBuilder.DropTable(
                name: "NotificationDeliveryAttempts");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "EscalationOccurrences");

            migrationBuilder.DropTable(
                name: "EscalationRules");

            migrationBuilder.DropTable(
                name: "EscalationPolicies");
        }
    }
}
