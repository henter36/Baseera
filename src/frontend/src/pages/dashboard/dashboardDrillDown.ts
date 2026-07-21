import type { DashboardOperationsFilters } from '../../api/client'

export function buildNotesDrillDown(filters: DashboardOperationsFilters & {
  overdueOnly?: boolean
  dueSoonDays?: number
  unassignedOnly?: boolean
  requiresRouting?: boolean
  sortBy?: string
  sortDesc?: boolean
}): string {
  const params = new URLSearchParams()
  if (filters.regionId) params.set('regionId', filters.regionId)
  if (filters.facilityId) params.set('facilityId', filters.facilityId)
  if (filters.facilityUnitId) params.set('facilityUnitId', filters.facilityUnitId)
  if (filters.noteTypeId) params.set('noteTypeId', filters.noteTypeId)
  if (filters.severity !== undefined) params.set('severity', String(filters.severity))
  if (filters.status !== undefined) params.set('status', String(filters.status))
  if (filters.overdueOnly) params.set('overdueOnly', 'true')
  if (filters.dueSoonDays !== undefined) params.set('dueSoonDays', String(filters.dueSoonDays))
  if (filters.unassignedOnly) params.set('unassignedOnly', 'true')
  if (filters.requiresRouting) params.set('requiresRouting', 'true')
  if (filters.sortBy) params.set('sortBy', filters.sortBy)
  if (filters.sortDesc !== undefined) {
    params.set('sortDesc', String(filters.sortDesc))
  }
  const query = params.toString()
  return query ? `/notes?${query}` : '/notes'
}

export function buildCorrectiveActionsDrillDown(filters: DashboardOperationsFilters & {
  overdueOnly?: boolean
  sortBy?: string
}): string {
  const params = new URLSearchParams()
  if (filters.regionId) params.set('regionId', filters.regionId)
  if (filters.facilityId) params.set('facilityId', filters.facilityId)
  if (filters.facilityUnitId) params.set('facilityUnitId', filters.facilityUnitId)
  if (filters.overdueOnly) params.set('overdueOnly', 'true')
  if (filters.sortBy) params.set('sortBy', filters.sortBy)
  const query = params.toString()
  return query ? `/corrective-actions?${query}` : '/corrective-actions'
}
