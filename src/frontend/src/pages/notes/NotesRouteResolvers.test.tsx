import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { NoteDetailRoute, NotesIndexRoute } from './NotesRouteResolvers'

const { listRegions, listFacilities, myNoteTypes, listNotes, getNote, getHistory, getAssignments, getAttachments } = vi.hoisted(() => ({
  listRegions: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  listFacilities: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  myNoteTypes: vi.fn(async () => []),
  listNotes: vi.fn(async () => ({ items: [], page: 1, pageSize: 20, totalCount: 0 })),
  getNote: vi.fn(async () => ({
    id: '11111111-1111-1111-1111-111111111111',
    referenceNumber: 'OBS-1',
    title: 'ملاحظة',
    description: 'وصف',
    status: 1,
    statusAr: 'مفتوحة',
    severity: 1,
    severityAr: 'متوسطة',
    noteTypeId: 't1',
    noteTypeCode: 'OPS',
    noteTypeNameAr: 'تشغيلية',
    noteTypeIsActive: true,
    sourceType: 0,
    sourceAr: 'يدوي',
    classification: 0,
    scopeType: 3,
    reportedByUserId: 'u1',
    reportedAtUtc: '2026-07-23T09:00:00Z',
    isOverdue: false,
    createdAtUtc: '2026-07-23T09:00:00Z',
    rowVersion: 'rv',
    isSensitiveRedacted: false,
  })),
  getHistory: vi.fn(async () => []),
  getAssignments: vi.fn(async () => []),
  getAttachments: vi.fn(async () => []),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Notes.View' || code === 'Notes.Create',
  useAuth: () => ({ hasPermission: (code: string) => code === 'Notes.View' || code === 'Notes.Create' }),
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      regions: listRegions,
      facilities: listFacilities,
      myNoteTypes,
      notes: {
        ...actual.api.notes,
        list: listNotes,
        get: getNote,
        history: getHistory,
        assignments: getAssignments,
        attachments: getAttachments,
      },
    },
  }
})

function renderAt(initialEntry: string, resolver: 'index' | 'detail') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/notes" element={resolver === 'index' ? <NotesIndexRoute /> : <div>NOT-USED</div>} />
          <Route path="/notes/:id" element={resolver === 'detail' ? <NoteDetailRoute /> : <div>NOT-USED</div>} />
          <Route path="/notes/workspace" element={<div>OBSERVATION_WORKSPACE</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('Notes route resolution (Phase 1A feature flag)', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  describe('with the flag enabled (default)', () => {
    it('navigates /notes to /notes/workspace', async () => {
      renderAt('/notes', 'index')
      expect(await screen.findByText('OBSERVATION_WORKSPACE')).toBeInTheDocument()
    })

    it('navigates /notes/:id to /notes/workspace?noteId=:id', async () => {
      renderAt('/notes/11111111-1111-1111-1111-111111111111', 'detail')
      expect(await screen.findByText('OBSERVATION_WORKSPACE')).toBeInTheDocument()
    })

    it('carries safe list filters over when redirecting /notes', async () => {
      renderAt('/notes?status=3&search=hello&page=2&unsafeParam=x', 'index')
      expect(await screen.findByText('OBSERVATION_WORKSPACE')).toBeInTheDocument()
    })
  })

  describe('with the flag explicitly disabled (rollback)', () => {
    beforeEach(() => {
      vi.stubEnv('VITE_OBSERVATION_WORKSPACE_V2', 'false')
    })

    it('keeps rendering the Legacy NotesListPage at /notes', async () => {
      renderAt('/notes', 'index')
      expect(await screen.findByText('ملاحظة جديدة')).toBeInTheDocument()
      expect(screen.queryByText('OBSERVATION_WORKSPACE')).not.toBeInTheDocument()
    })

    it('keeps rendering the Legacy NoteDetailPage at /notes/:id', async () => {
      renderAt('/notes/11111111-1111-1111-1111-111111111111', 'detail')
      expect(await screen.findByText('OBS-1')).toBeInTheDocument()
      expect(screen.queryByText('OBSERVATION_WORKSPACE')).not.toBeInTheDocument()
    })
  })
})
