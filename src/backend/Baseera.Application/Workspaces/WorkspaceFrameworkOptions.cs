namespace Baseera.Application.Workspaces;

public sealed class WorkspaceFrameworkOptions
{
    public const int DefaultWidgetQueryBudget = 8;
    public const int MaximumWidgetQueryBudget = 32;

    public int WidgetQueryBudget { get; init; } = DefaultWidgetQueryBudget;

    public int EffectiveWidgetQueryBudget => Math.Clamp(WidgetQueryBudget, 1, MaximumWidgetQueryBudget);
}
