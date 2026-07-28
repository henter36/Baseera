// Controls Route resolution for the legacy /notes and /notes/:id URLs, not just nav-link
// visibility (docs/ux-rescue/phase1a-observation-route-transition.md). Defaults to enabled so the
// Workspace is the primary experience; set VITE_OBSERVATION_WORKSPACE_V2=false at build time to
// roll back to the legacy NotesListPage/NoteDetailPage instantly without a code change.
export function isObservationWorkspaceV2Enabled(): boolean {
  return import.meta.env.VITE_OBSERVATION_WORKSPACE_V2 !== 'false'
}

// Query params that are safe to carry over from the legacy /notes list URL into
// /notes/workspace verbatim (same name, same meaning, never sensitive).
const CARRY_OVER_LIST_PARAMS = [
  'search',
  'status',
  'severity',
  'noteTypeId',
  'classification',
  'regionId',
  'facilityId',
  'facilityUnitId',
  'ownerDepartmentId',
  'overdueOnly',
  'requiresMyAction',
  'requiresRouting',
  'sortBy',
  'sortDesc',
  'page',
] as const

export function buildWorkspaceRedirectSearch(source: URLSearchParams): string {
  const next = new URLSearchParams()
  for (const key of CARRY_OVER_LIST_PARAMS) {
    const value = source.get(key)
    if (value) next.set(key, value)
  }
  return next.toString()
}

export function buildWorkspaceNoteRedirectSearch(noteId: string, source: URLSearchParams): string {
  const next = new URLSearchParams(buildWorkspaceRedirectSearch(source))
  next.set('noteId', noteId)
  next.set('source', 'legacy-link')
  return next.toString()
}
