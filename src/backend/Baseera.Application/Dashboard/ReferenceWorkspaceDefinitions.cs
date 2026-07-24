namespace Baseera.Application.Dashboard;

using Baseera.Application.Workspaces;
using Baseera.Domain.Identity;

public sealed class ReferenceWorkspaceDefinitionProvider : IWorkspaceDefinitionProvider
{
    public const string WorkspaceKey = "reference";
    public const string OperationalSummaryWidgetKey = "dashboard.operational-summary";
    public const string CorrectiveActionsWidgetKey = "dashboard.corrective-actions-summary";

    public WorkspaceDefinition Definition { get; } = new(
        WorkspaceKey,
        "مساحة عمل مرجعية",
        "Reference Workspace",
        new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain, WorkspaceLevel.Facility, WorkspaceLevel.Region, WorkspaceLevel.Headquarters },
        new HashSet<string> { PermissionCodes.WorkspacesView },
        [OperationalSummaryWidgetKey, CorrectiveActionsWidgetKey],
        new WorkspaceLayoutDefinition(
            [
                new WorkspaceLayoutItemDefinition(OperationalSummaryWidgetKey, 1, WidgetSize.Wide, true),
                new WorkspaceLayoutItemDefinition(CorrectiveActionsWidgetKey, 2, WidgetSize.Medium, false)
            ],
            1),
        [
            new WorkspaceFilterDefinition("fromUtc", "من تاريخ", "date", true),
            new WorkspaceFilterDefinition("toUtc", "إلى تاريخ", "date", true),
            new WorkspaceFilterDefinition("regionId", "المنطقة", "region", true),
            new WorkspaceFilterDefinition("facilityId", "المنشأة", "facility", true)
        ],
        [
            new DrillDownDefinition("dashboard.operations", "لوحة المتابعة", PermissionCodes.DashboardViewOperational),
            new DrillDownDefinition("corrective-actions.list", "الإجراءات التصحيحية", PermissionCodes.CorrectiveActionsView)
        ],
        new WorkspaceFeatureAvailability(false, false, false, true),
        1);
}

public sealed record ReferenceOperationalSummaryPayload(
    int OpenNotes,
    int InProgressNotes,
    int PendingVerificationNotes,
    int UnassignedNotes,
    int RequiresRouting,
    int OverdueNotes,
    int DueSoonNotes,
    int CriticalOrHighNotes);

public sealed record ReferenceCorrectiveActionsPayload(
    int ActiveActions,
    int OverdueActions,
    int PendingVerificationActions,
    int ReopenedActions,
    int NotesWithStalledActions);
