import type { NoteListFilters } from '../../api/client'

type SearchParamValue =
  | string
  | number
  | boolean
  | undefined

function appendParam(
  params: URLSearchParams,
  key: string,
  value: SearchParamValue,
): void {
  if (
    value === undefined ||
    value === '' ||
    value === false
  ) {
    return
  }

  params.set(
    key,
    value === true ? 'true' : String(value),
  )
}

export function buildNotesListSearchParams(
  filters: NoteListFilters,
): URLSearchParams {
  const params = new URLSearchParams()

  const values: Array<
    [string, SearchParamValue]
  > = [
    ['search', filters.search],
    ['status', filters.status],
    ['severity', filters.severity],
    ['noteTypeId', filters.noteTypeId],
    ['classification', filters.classification],
    ['regionId', filters.regionId],
    ['facilityId', filters.facilityId],
    ['facilityUnitId', filters.facilityUnitId],
    ['ownerDepartmentId', filters.ownerDepartmentId],
    ['overdueOnly', filters.overdueOnly],
    ['dueSoonDays', filters.dueSoonDays],
    ['unassignedOnly', filters.unassignedOnly],
    ['requiresMyAction', filters.requiresMyAction],
    ['requiresRouting', filters.requiresRouting],
  ]

  values.forEach(([key, value]) => {
    appendParam(params, key, value)
  })

  appendParam(
    params,
    'page',
    filters.page != null && filters.page > 1
      ? filters.page
      : undefined,
  )

  appendParam(
    params,
    'sortBy',
    filters.sortBy &&
      filters.sortBy !== 'createdAtUtc'
      ? filters.sortBy
      : undefined,
  )

  if (filters.sortDesc === false) {
    params.set('sortDesc', 'false')
  }

  return params
}
