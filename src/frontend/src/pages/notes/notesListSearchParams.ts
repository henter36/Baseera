import type { NoteListFilters } from '../../api/client'

export function buildNotesListSearchParams(filters: NoteListFilters): URLSearchParams {
  const params = new URLSearchParams()
  if (filters.search) params.set('search', filters.search)
  if (filters.status != null) params.set('status', String(filters.status))
  if (filters.severity != null) params.set('severity', String(filters.severity))
  if (filters.noteTypeId) params.set('noteTypeId', filters.noteTypeId)
  if (filters.classification != null) params.set('classification', String(filters.classification))
  if (filters.regionId) params.set('regionId', filters.regionId)
  if (filters.facilityId) params.set('facilityId', filters.facilityId)
  if (filters.facilityUnitId) params.set('facilityUnitId', filters.facilityUnitId)
  if (filters.ownerDepartmentId) params.set('ownerDepartmentId', filters.ownerDepartmentId)
  if (filters.overdueOnly) params.set('overdueOnly', 'true')
  if (filters.dueSoonDays != null) params.set('dueSoonDays', String(filters.dueSoonDays))
  if (filters.unassignedOnly) params.set('unassignedOnly', 'true')
  if (filters.requiresMyAction) params.set('requiresMyAction', 'true')
  if (filters.requiresRouting) params.set('requiresRouting', 'true')
  if (filters.page != null && filters.page > 1) params.set('page', String(filters.page))
  if (filters.sortBy && filters.sortBy !== 'createdAtUtc') params.set('sortBy', filters.sortBy)
  if (filters.sortDesc === false) params.set('sortDesc', 'false')
  return params
}
