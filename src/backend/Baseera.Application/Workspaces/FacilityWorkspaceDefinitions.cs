namespace Baseera.Application.Workspaces;

using Baseera.Domain.Identity;

public sealed class FacilityWorkspaceDefinitionProvider : IWorkspaceDefinitionProvider
{
    public const string WorkspaceKey = "facility-operations";
    public const string HeaderWidgetKey = "facility.header";
    public const string ExecutiveSummaryWidgetKey = "facility.executive-summary";
    public const string NotesOverviewWidgetKey = "facility.notes-overview";
    public const string CorrectiveActionsWidgetKey = "facility.corrective-actions";
    public const string AlertsEscalationsWidgetKey = "facility.alerts-escalations";
    public const string FormComplianceWidgetKey = "facility.form-compliance";
    public const string PriorityQueueWidgetKey = "facility.priority-queue";
    public const string RecentActivityWidgetKey = "facility.recent-activity";

    public WorkspaceDefinition Definition { get; } = new(
        WorkspaceKey,
        "مركز قرار السجن",
        "Facility Decision Center",
        new HashSet<WorkspaceLevel> { WorkspaceLevel.Facility },
        new HashSet<string> { PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewFacility },
        [
            HeaderWidgetKey,
            ExecutiveSummaryWidgetKey,
            PriorityQueueWidgetKey,
            NotesOverviewWidgetKey,
            CorrectiveActionsWidgetKey,
            AlertsEscalationsWidgetKey,
            FormComplianceWidgetKey,
            RecentActivityWidgetKey
        ],
        new WorkspaceLayoutDefinition(
            [
                new WorkspaceLayoutItemDefinition(HeaderWidgetKey, 0, WidgetSize.Wide, true),
                new WorkspaceLayoutItemDefinition(ExecutiveSummaryWidgetKey, 1, WidgetSize.Large, true),
                new WorkspaceLayoutItemDefinition(PriorityQueueWidgetKey, 2, WidgetSize.Large, true),
                new WorkspaceLayoutItemDefinition(NotesOverviewWidgetKey, 3, WidgetSize.Medium, false),
                new WorkspaceLayoutItemDefinition(CorrectiveActionsWidgetKey, 4, WidgetSize.Medium, false),
                new WorkspaceLayoutItemDefinition(AlertsEscalationsWidgetKey, 5, WidgetSize.Medium, false),
                new WorkspaceLayoutItemDefinition(FormComplianceWidgetKey, 6, WidgetSize.Medium, false),
                new WorkspaceLayoutItemDefinition(RecentActivityWidgetKey, 7, WidgetSize.Wide, false)
            ],
            1),
        [
            new WorkspaceFilterDefinition("fromUtc", "من تاريخ", "date", true),
            new WorkspaceFilterDefinition("toUtc", "إلى تاريخ", "date", true),
            new WorkspaceFilterDefinition("status", "الحالة", "status", true),
            new WorkspaceFilterDefinition("severity", "الخطورة", "severity", true)
        ],
        [
            new DrillDownDefinition("notes.workspace", "مساحة عمل الملاحظات", PermissionCodes.NotesView),
            new DrillDownDefinition("corrective-actions.list", "الإجراءات التصحيحية", PermissionCodes.CorrectiveActionsView),
            new DrillDownDefinition("escalations.occurrences", "حوادث التصعيد", PermissionCodes.EscalationsViewOccurrences),
            new DrillDownDefinition("form-compliance.facility", "التزام النماذج للسجن", PermissionCodes.FormsViewComplianceDashboard),
            new DrillDownDefinition("dashboard.operations", "لوحة المتابعة", PermissionCodes.DashboardViewOperational)
        ],
        new WorkspaceFeatureAvailability(false, false, false, false),
        1);
}

internal static class FacilityWorkspaceWidgetDefinitions
{
    public static WidgetDefinition Create(
        string key,
        string titleAr,
        string titleEn,
        string descriptionAr,
        WidgetCategory category,
        string requiredPermission,
        string dataCapability,
        WidgetSize size,
        bool sensitive = false) =>
        new(
            key,
            titleAr,
            titleEn,
            descriptionAr,
            category,
            new HashSet<WorkspaceLevel> { WorkspaceLevel.Facility },
            requiredPermission,
            dataCapability,
            size,
            WidgetSize.Small,
            WidgetSize.Wide,
            new WidgetRefreshPolicy(60, true),
            new WidgetDataFreshnessPolicy(300, 1800, 3600),
            new WidgetEmptyErrorBehavior("لا توجد بيانات ضمن هذا السجن.", $"تعذر تحميل {titleAr}.", true),
            true,
            false,
            sensitive,
            true);
}

