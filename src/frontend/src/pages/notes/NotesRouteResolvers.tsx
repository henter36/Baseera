import { Navigate, useParams, useSearchParams } from 'react-router'
import { NoteDetailPage } from './NoteDetailPage'
import { NotesListPage } from './NotesListPage'
import { buildWorkspaceNoteRedirectSearch, buildWorkspaceRedirectSearch, isObservationWorkspaceV2Enabled } from './observationWorkspaceFlag'

// Route-resolution level compatibility for the legacy /notes URL (docs/ux-rescue/phase1a-observation-route-transition.md):
// this is a client-side <Navigate>, not an HTTP redirect — a server/CDN-level 301/302 is a separate,
// optional concern if ever needed for external links. When VITE_OBSERVATION_WORKSPACE_V2=false the
// Legacy NotesListPage still renders here, so rollback never depends on deleting anything.
export function NotesIndexRoute() {
  const [searchParams] = useSearchParams()
  if (!isObservationWorkspaceV2Enabled()) {
    return <NotesListPage />
  }
  const search = buildWorkspaceRedirectSearch(searchParams)
  return <Navigate to={`/notes/workspace${search ? `?${search}` : ''}`} replace />
}

// /notes/:id used to be the only note detail URL (still linked from old notifications, emails,
// bookmarks); it must keep working. With the flag on it opens the same note inside the Workspace
// instead of navigating to a separate page.
export function NoteDetailRoute() {
  const { id } = useParams<{ id: string }>()
  const [searchParams] = useSearchParams()
  if (!isObservationWorkspaceV2Enabled() || !id) {
    return <NoteDetailPage />
  }
  const search = buildWorkspaceNoteRedirectSearch(id, searchParams)
  return <Navigate to={`/notes/workspace?${search}`} replace />
}
