namespace Baseera.Application.Workspaces;

internal static class FacilityWorkspaceContextGuard
{
    public static Guid RequireFacilityId(WorkspaceContext context) =>
        context.FacilityId ?? throw new ArgumentException("facilityId مطلوب لمساحة عمل المنشأة.");
}
