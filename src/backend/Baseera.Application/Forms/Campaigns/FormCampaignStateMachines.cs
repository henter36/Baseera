namespace Baseera.Application.Forms.Campaigns;

using Baseera.Domain.Forms;

public static class FormCampaignStateMachine
{
    private static readonly HashSet<(FormCampaignStatus From, FormCampaignStatus To)> Transitions =
    [
        (FormCampaignStatus.Draft, FormCampaignStatus.Scheduled),
        (FormCampaignStatus.Draft, FormCampaignStatus.Active),
        (FormCampaignStatus.Scheduled, FormCampaignStatus.Active),
        (FormCampaignStatus.Scheduled, FormCampaignStatus.Paused),
        (FormCampaignStatus.Active, FormCampaignStatus.Paused),
        (FormCampaignStatus.Paused, FormCampaignStatus.Scheduled),
        (FormCampaignStatus.Paused, FormCampaignStatus.Active),
        (FormCampaignStatus.Scheduled, FormCampaignStatus.Cancelled),
        (FormCampaignStatus.Active, FormCampaignStatus.Cancelled),
        (FormCampaignStatus.Paused, FormCampaignStatus.Cancelled),
        (FormCampaignStatus.Scheduled, FormCampaignStatus.Completed),
        (FormCampaignStatus.Active, FormCampaignStatus.Completed)
    ];

    public static bool CanTransition(FormCampaignStatus from, FormCampaignStatus to) =>
        Transitions.Contains((from, to));

    public static void EnsureCanTransition(FormCampaignStatus from, FormCampaignStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException($"انتقال حالة الحملة غير مسموح من {from} إلى {to}.");
        }
    }

    public static bool IsMutable(FormCampaignStatus status) => status == FormCampaignStatus.Draft;

    public static bool CanGenerateCycles(FormCampaignStatus status) =>
        status is FormCampaignStatus.Scheduled or FormCampaignStatus.Active;
}

public static class FormCycleStateMachine
{
    private static readonly HashSet<(FormCycleStatus From, FormCycleStatus To)> Transitions =
    [
        (FormCycleStatus.Scheduled, FormCycleStatus.Open),
        (FormCycleStatus.Open, FormCycleStatus.Grace),
        (FormCycleStatus.Grace, FormCycleStatus.Closed),
        (FormCycleStatus.Scheduled, FormCycleStatus.Cancelled),
        (FormCycleStatus.Open, FormCycleStatus.Cancelled),
        (FormCycleStatus.Grace, FormCycleStatus.Cancelled),
        (FormCycleStatus.Open, FormCycleStatus.Closed),
        (FormCycleStatus.Scheduled, FormCycleStatus.Closed)
    ];

    public static bool CanTransition(FormCycleStatus from, FormCycleStatus to) =>
        Transitions.Contains((from, to));

    public static void EnsureCanTransition(FormCycleStatus from, FormCycleStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException($"انتقال حالة الدورة غير مسموح من {from} إلى {to}.");
        }
    }

    public static FormCycleStatus ResolveStatus(
        DateTimeOffset utcNow,
        DateTimeOffset openAtUtc,
        DateTimeOffset dueAtUtc,
        DateTimeOffset graceEndsAtUtc,
        DateTimeOffset closeAtUtc,
        FormCycleStatus current)
    {
        if (current is FormCycleStatus.Cancelled or FormCycleStatus.Closed)
        {
            return current;
        }

        if (utcNow >= closeAtUtc)
        {
            return FormCycleStatus.Closed;
        }

        if (utcNow >= graceEndsAtUtc || (utcNow >= dueAtUtc && graceEndsAtUtc > dueAtUtc))
        {
            if (utcNow >= dueAtUtc && utcNow < graceEndsAtUtc)
            {
                return FormCycleStatus.Grace;
            }

            if (utcNow >= graceEndsAtUtc)
            {
                return FormCycleStatus.Closed;
            }
        }

        if (utcNow >= openAtUtc)
        {
            return utcNow >= dueAtUtc && utcNow < graceEndsAtUtc
                ? FormCycleStatus.Grace
                : FormCycleStatus.Open;
        }

        return FormCycleStatus.Scheduled;
    }
}
