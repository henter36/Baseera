import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ObservationWorkspacePage } from './ObservationWorkspacePage'

const { workspace, workspaceDetail, listRegions, listFacilities } = vi.hoisted(() => ({
  workspace: vi.fn(),
  workspaceDetail: vi.fn(),
  listRegions: vi.fn(async () => ({ items: [], page: 1, pageSize: 20, totalCount: 0 })),
  listFacilities: vi.fn(async () => ({ items: [], page: 1, pageSize: 20, totalCount: 0 })),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Notes.View' || code === 'Notes.Create',
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      regions: listRegions,
      facilities: listFacilities,
      notes: { ...actual.api.notes, workspace, workspaceDetail },
    },
  }
})

const note = {
  id: '11111111-1111-1111-1111-111111111111',
  referenceNumber: 'OBS-00000024',
  title: 'تعطل إنارة الممر الرئيسي',
  descriptionSnippet: null,
  status: 3,
  statusAr: 'قيد المعالجة',
  severity: 2,
  severityAr: 'عالية',
  noteTypeId: 'type-1',
  noteTypeCode: 'OPS',
  noteTypeNameAr: 'تشغيلية',
  noteTypeIsActive: true,
  classification: 0,
  scopeType: 3,
  regionId: 'region-1',
  facilityId: 'facility-1',
  facilityUnitId: null,
  dueAtUtc: '2026-07-24T09:00:00Z',
  isOverdue: true,
  currentAssigneeDisplay: 'فريق الصيانة',
  createdAtUtc: '2026-07-23T09:00:00Z',
  rowVersion: 'rv',
  isSensitiveRedacted: false,
}

const detail = {
  note: {
    ...note,
    description: 'الإنارة متوقفة في الممر الرئيسي وتحتاج معالجة عاجلة.',
    noteTypeDescriptionAr: null,
    noteTypeEntryInstructionsAr: null,
    sourceType: 0,
    sourceAr: 'يدوي',
    sourceReference: null,
    ownerDepartmentId: null,
    reportedByUserId: 'user-1',
    reportedByDisplayName: 'مشرف الموقع',
    reportedAtUtc: '2026-07-23T09:00:00Z',
    submittedAtUtc: '2026-07-23T09:10:00Z',
    workStartedAtUtc: '2026-07-23T10:00:00Z',
    submittedForVerificationAtUtc: null,
    closedAtUtc: null,
    closedByUserId: null,
    closureSummary: null,
    reopenedAtUtc: null,
    reopenReason: null,
    currentAssignment: null,
  },
  allowedActions: ['ADD_ACTION', 'REQUEST_VERIFICATION'],
  summary: {
    openCorrectiveActions: 1,
    attachmentCount: 0,
    waitingResource: false,
    waitingVerification: false,
    waitingClosureApproval: false,
    hasEscalation: false,
    progressPercent: 55,
    currentBlockerAr: 'متجاوزة للموعد',
    lastUpdatedAtUtc: '2026-07-23T10:00:00Z',
  },
  assignments: [],
  correctiveActions: { items: [], page: 1, pageSize: 10, totalCount: 0 },
  attachments: [],
  resources: [],
  decisions: [],
  links: [],
  timeline: [{
    id: 'timeline-1',
    type: 'STATUS',
    titleAr: 'تغيير الحالة إلى قيد المعالجة',
    descriptionAr: 'بدء العمل',
    actorDisplayName: 'فريق الصيانة',
    occurredAtUtc: '2026-07-23T10:00:00Z',
    tone: 'muted',
  }],
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <ObservationWorkspacePage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('ObservationWorkspacePage', () => {
  beforeEach(() => {
    workspace.mockReset()
    workspaceDetail.mockReset()
    listRegions.mockClear()
    listFacilities.mockClear()
    workspace.mockResolvedValue({ notes: { items: [note], page: 1, pageSize: 20, totalCount: 1 } })
    workspaceDetail.mockResolvedValue(detail)
  })

  it('keeps operators in one master-detail workspace and renders server allowed actions', async () => {
    renderPage()

    const card = await screen.findByRole('button', { name: /OBS-00000024/ })
    expect(within(card).getByText('تعطل إنارة الممر الرئيسي')).toBeInTheDocument()
    expect(within(card).getByText('متأخرة')).toBeInTheDocument()

    await userEvent.click(card)

    expect(await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'إضافة إجراء' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'طلب تحقق' })).toBeInTheDocument()
    expect(screen.getByText('الإنارة متوقفة في الممر الرئيسي وتحتاج معالجة عاجلة.')).toBeInTheDocument()
  })

  it('sends workspace filters to the server-side query', async () => {
    renderPage()
    await screen.findByText('تعطل إنارة الممر الرئيسي')

    await userEvent.selectOptions(screen.getByLabelText('الحالة'), '3')
    await userEvent.click(screen.getByLabelText('المتأخرة'))

    await waitFor(() => {
      const lastCall = workspace.mock.calls.at(-1)?.[0]
      expect(lastCall).toMatchObject({ status: 3, overdueOnly: true })
    })
  })
})
