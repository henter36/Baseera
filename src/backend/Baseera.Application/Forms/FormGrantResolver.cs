namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;

internal static class FormGrantResolver
{
    /// <summary>
    /// Resolves effective grant for a capability. Deny beats allow; null means no grant decision.
    /// </summary>
    public static bool? ResolveEffectiveGrant(
        IEnumerable<FormAccessGrant> grants,
        FormAccessCapability capability,
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        FormDefinition form,
        DateTimeOffset now)
    {
        var applicable = grants
            .Where(g => g.Capability == capability && IsWithinValidityWindow(g, now) && MatchesPrincipal(g, userId, roleIds))
            .Where(g => GrantScopeCoversForm(g, form))
            .ToList();

        if (applicable.Any(g => g.Effect == FormAccessGrantEffect.Deny))
        {
            return false;
        }

        if (applicable.Any(g => g.Effect == FormAccessGrantEffect.Allow))
        {
            return true;
        }

        return null;
    }

    public static bool IsWithinValidityWindow(FormAccessGrant grant, DateTimeOffset now)
    {
        if (grant.ValidFromUtc.HasValue && now < grant.ValidFromUtc.Value)
        {
            return false;
        }

        if (grant.ValidToUtc.HasValue && now > grant.ValidToUtc.Value)
        {
            return false;
        }

        return true;
    }

    public static bool MatchesPrincipal(FormAccessGrant grant, Guid userId, IReadOnlyCollection<Guid> roleIds) =>
        grant.PrincipalType switch
        {
            FormAccessGrantPrincipalType.User => grant.PrincipalId == userId,
            FormAccessGrantPrincipalType.Role => roleIds.Contains(grant.PrincipalId),
            _ => false
        };

    public static bool GrantScopeCoversForm(FormAccessGrant grant, FormDefinition form)
    {
        if (!grant.ScopeType.HasValue)
        {
            return true;
        }

        return grant.ScopeType.Value switch
        {
            ScopeType.Global => form.ScopeType is ScopeType.Global,
            ScopeType.Headquarters => form.ScopeType is ScopeType.Global or ScopeType.Headquarters,
            ScopeType.Region => form.ScopeType == ScopeType.Region &&
                                grant.RegionId.HasValue &&
                                form.RegionId == grant.RegionId,
            ScopeType.Facility => form.ScopeType is ScopeType.Facility or ScopeType.FacilityUnit &&
                                  grant.FacilityId.HasValue &&
                                  form.FacilityId == grant.FacilityId,
            _ => false
        };
    }

    public static bool GrantScopeWithinGrantorScope(
        FormAccessGrant grant,
        IOrganizationalScopeService orgScope)
    {
        if (!grant.ScopeType.HasValue)
        {
            return true;
        }

        return grant.ScopeType.Value switch
        {
            ScopeType.Global => orgScope.HasNationalAccess,
            ScopeType.Headquarters => orgScope.HasHeadquartersAccess,
            ScopeType.Region => grant.RegionId.HasValue && orgScope.CanAccessRegion(grant.RegionId.Value),
            ScopeType.Facility => grant.FacilityId.HasValue && orgScope.CanAccessFacility(grant.FacilityId.Value),
            _ => false
        };
    }
}
