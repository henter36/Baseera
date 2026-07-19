import type { Me } from '../api/client'
import { ScopeType } from './noteEnums'

/**
 * Determines which OperationalNote ScopeType values the current user is allowed to pick when
 * creating a note, mirroring the reachability rules in
 * Baseera.Application.Security.OrganizationalScopeService (a Region scope holder can also reach
 * Facility/FacilityUnit scopes nested under their region, etc.). The backend is the final
 * authority (NoteCommandService re-checks via INoteScopeService.CanAccess).
 */
export function allowedScopeTypes(me: Me | null): number[] {
  if (!me) return []
  const scopeTypes = new Set(me.scopes.filter((s) => s.isActive).map((s) => s.scopeType))

  if (scopeTypes.has(ScopeType.Global)) {
    return [ScopeType.Global, ScopeType.Headquarters, ScopeType.Region, ScopeType.Facility, ScopeType.FacilityUnit]
  }

  const allowed = new Set<number>()
  if (scopeTypes.has(ScopeType.Headquarters)) {
    allowed.add(ScopeType.Headquarters)
  }
  if (scopeTypes.has(ScopeType.Region) || scopeTypes.has(ScopeType.MultipleRegions)) {
    allowed.add(ScopeType.Region)
    allowed.add(ScopeType.Facility)
    allowed.add(ScopeType.FacilityUnit)
  }
  if (
    scopeTypes.has(ScopeType.Facility) ||
    scopeTypes.has(ScopeType.MultipleFacilities) ||
    scopeTypes.has(ScopeType.FacilityUnit)
  ) {
    allowed.add(ScopeType.Facility)
    allowed.add(ScopeType.FacilityUnit)
  }

  return Array.from(allowed).sort((a, b) => a - b)
}
