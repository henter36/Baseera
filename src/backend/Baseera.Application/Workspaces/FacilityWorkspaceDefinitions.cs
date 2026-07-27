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
    public const string OccupancyWidgetKey = "facility.occupancy";
    public const string ResourcesWidgetKey = "facility.resources";
    public const string WorkforceWidgetKey = "facility.workforce";
    public const string SensitiveCustodyWidgetKey = "facility.sensitive-custody";
    public const string PriorityQueueWidgetKey = "facility.priority-queue";
    public const string RecentActivityWidgetKey = "facility.recent-activity";
    public const string StructureWidgetKey = "facility.structure";
    public const string DataQualityWidgetKey = "facility.data-quality";

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
            OccupancyWidgetKey,
            ResourcesWidgetKey,
            WorkforceWidgetKey,
            SensitiveCustodyWidgetKey,
            NotesOverviewWidgetKey,
            CorrectiveActionsWidgetKey,
            AlertsEscalationsWidgetKey,
            FormComplianceWidgetKey,
            StructureWidgetKey,
            RecentActivityWidgetKey,
            DataQualityWidgetKey
        ],
        new WorkspaceLayoutDefinition(
            [
                new WorkspaceLayoutItemDefinition(HeaderWidgetKey, 0, WidgetSize.Wide, true),
                new WorkspaceLayoutItemDefinition(ExecutiveSummaryWidgetKey, 1, WidgetSize.Large, true),
                new WorkspaceLayoutItemDefinition(PriorityQueueWidgetKey, 2, WidgetSize.Large, true),
                new WorkspaceLayoutItemDefinition(OccupancyWidgetKey, 3, WidgetSize.Wide, false),
                new WorkspaceLayoutItemDefinition(ResourcesWidgetKey, 4, WidgetSize.Wide, false),
                new WorkspaceLayoutItemDefinition(WorkforceWidgetKey, 5, WidgetSize.Wide, false),
                new WorkspaceLayoutItemDefinition(SensitiveCustodyWidgetKey, 6, WidgetSize.Wide, false),
                new WorkspaceLayoutItemDefinition(NotesOverviewWidgetKey, 7, WidgetSize.Medium, false),
                new WorkspaceLayoutItemDefinition(CorrectiveActionsWidgetKey, 8, WidgetSize.Medium, false),
                new WorkspaceLayoutItemDefinition(AlertsEscalationsWidgetKey, 9, WidgetSize.Medium, false),
                new WorkspaceLayoutItemDefinition(FormComplianceWidgetKey, 10, WidgetSize.Medium, false),
                new WorkspaceLayoutItemDefinition(StructureWidgetKey, 11, WidgetSize.Wide, false),
                new WorkspaceLayoutItemDefinition(RecentActivityWidgetKey, 12, WidgetSize.Wide, false),
                new WorkspaceLayoutItemDefinition(DataQualityWidgetKey, 13, WidgetSize.Wide, false)
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
            new DrillDownDefinition("facility.occupancy", "إشغال السجن", PermissionCodes.OccupancyViewSummary),
            new DrillDownDefinition("facility.resources", "موارد السجن", PermissionCodes.ResourcesViewSummary),
            new DrillDownDefinition("facility.workforce", "القوى البشرية والتغطية", PermissionCodes.WorkforceViewSummary),
            new DrillDownDefinition("facility.sensitive-custody", "الأسلحة والعهد الحساسة", PermissionCodes.SensitiveCustodyViewSummary),
            new DrillDownDefinition("dashboard.operations", "لوحة المتابعة", PermissionCodes.DashboardViewOperational)
        ],
        new WorkspaceFeatureAvailability(false, false, false, false),
        1);
}

internal static class FacilityWorkspaceWidgetDefinitions
{
    public static WidgetDefinition Create(FacilityWorkspaceWidgetDefinitionSpec spec) =>
        new(
            spec.Key,
            spec.TitleAr,
            spec.TitleEn,
            spec.DescriptionAr,
            spec.Category,
            new HashSet<WorkspaceLevel> { WorkspaceLevel.Facility },
            spec.RequiredPermission,
            spec.DataCapability,
            spec.Size,
            WidgetSize.Small,
            WidgetSize.Wide,
            new WidgetRefreshPolicy(60, true),
            new WidgetDataFreshnessPolicy(300, 1800, 3600),
            new WidgetEmptyErrorBehavior("لا توجد بيانات ضمن هذا السجن.", $"تعذر تحميل {spec.TitleAr}.", true),
            true,
            false,
            spec.Sensitive,
            true);
}

internal sealed record FacilityWorkspaceWidgetDefinitionSpec
{
    public required string Key { get; init; }
    public required string TitleAr { get; init; }
    public required string TitleEn { get; init; }
    public required string DescriptionAr { get; init; }
    public required WidgetCategory Category { get; init; }
    public required string RequiredPermission { get; init; }
    public required string DataCapability { get; init; }
    public required WidgetSize Size { get; init; }
    public bool Sensitive { get; init; }
}
