import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, MemoryRouter, RouterProvider } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ObservationWorkspacePage } from './ObservationWorkspacePage'

const { workspace, workspaceDetail, listRegions, listFacilities, listFacilityUnits, listNoteTypes, eligibleAssignees, assign, verifyClosure, uploadAttachment } = vi.hoisted(() => ({
  workspace: vi.fn(),
  workspaceDetail: vi.fn(),
  listRegions: vi.fn(async () => ({ items: [], page: 1, pageSize: 20, totalCount: 0 })),
  listFacilities: vi.fn(async () => ({ items: [], page: 1, pageSize: 20, totalCount: 0 })),
  listFacilityUnits: vi.fn(async () => ({ items: [], page: 1, pageSize: 20, totalCount: 0 })),
  listNoteTypes: vi.fn(async () => []),
  eligibleAssignees: vi.fn(async () => []),
  assign: vi.fn(),
  verifyClosure: vi.fn(),
  uploadAttachment: vi.fn(),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Notes.View' || code === 'Notes.Create' || code === 'Attachments.Upload',
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      regions: listRegions,
      facilities: listFacilities,
      facilityUnits: listFacilityUnits,
      noteTypes: listNoteTypes,
      notes: {
        ...actual.api.notes,
        workspace,
        workspaceDetail,
        eligibleAssignees,
        assign,
        verifyClosure,
      },
      uploadAttachment,
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

const secondNote = {
  ...note,
  id: '22222222-2222-2222-2222-222222222222',
  referenceNumber: 'OBS-00000025',
  title: 'تسرب مياه في غرفة الخدمات',
  rowVersion: 'rv-2',
}

const secondDetail = {
  ...detail,
  note: {
    ...detail.note,
    ...secondNote,
    description: 'تسرب مياه يحتاج عزل مصدر التغذية.',
  },
}

function renderPage(initialEntry = '/', queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })) {
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <ObservationWorkspacePage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function renderPageWithRouter(initialEntry = '/notes/workspace') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([
    { path: '/notes/workspace', element: <ObservationWorkspacePage /> },
  ], { initialEntries: [initialEntry] })

  const view = render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )

  return { ...view, router, queryClient }
}

describe('ObservationWorkspacePage', () => {
  beforeEach(() => {
    workspace.mockReset()
    workspaceDetail.mockReset()
    listRegions.mockClear()
    listFacilities.mockClear()
    listFacilityUnits.mockClear()
    listNoteTypes.mockClear()
    eligibleAssignees.mockReset()
    assign.mockReset()
    verifyClosure.mockReset()
    uploadAttachment.mockReset()
    workspace.mockResolvedValue({ notes: { items: [note], page: 1, pageSize: 20, totalCount: 1 } })
    workspaceDetail.mockResolvedValue(detail)
    uploadAttachment.mockResolvedValue({ id: 'attachment-1' })
  })

  it('keeps operators in one master-detail workspace and renders server allowed actions', async () => {
    renderPage()

    expect(await screen.findByText('تعطل إنارة الممر الرئيسي')).toBeInTheDocument()
    const card = screen.getByRole('button', { name: /OBS-00000024/ })
    expect(within(card).getByText('تعطل إنارة الممر الرئيسي')).toBeInTheDocument()
    expect(within(card).getByText('متأخرة')).toBeInTheDocument()

    await userEvent.click(card)

    expect(await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })).toBeInTheDocument()
    // Primary action is the first server-returned action; the rest render as secondary.
    expect(screen.getByRole('button', { name: 'إضافة إجراء' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'طلب تحقق' })).toBeInTheDocument()
    expect(screen.getByText('الإنارة متوقفة في الممر الرئيسي وتحتاج معالجة عاجلة.')).toBeInTheDocument()
  })

  it('renders observation detail as in-page master-detail content, not as modal or overlay', async () => {
    renderPage('/notes/workspace?noteId=11111111-1111-1111-1111-111111111111')

    expect(await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })).toBeInTheDocument()
    expect(screen.getByTestId('observation-master-detail-layout')).toBeInTheDocument()
    expect(screen.getByTestId('observation-list-pane')).toBeInTheDocument()
    expect(screen.getByTestId('observation-detail-pane')).toContainElement(screen.getByTestId('observation-detail-document-flow'))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.queryByRole('dialog', { hidden: true })).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/إغلاق لوحة التفاصيل/)).not.toBeInTheDocument()
    expect(document.querySelector('[aria-modal="true"]')).not.toBeInTheDocument()
    expect(document.querySelector('.workspace-detail-backdrop, .modal-backdrop, .drawer-backdrop, .overlay')).not.toBeInTheDocument()
    expect(document.body.style.overflow).not.toBe('hidden')
  })

  it('sends workspace filters to the server-side query', async () => {
    renderPage()
    await screen.findByText('تعطل إنارة الممر الرئيسي')

    await userEvent.selectOptions(screen.getByLabelText('الحالة'), '3')
    await userEvent.selectOptions(screen.getByLabelText('الاستحقاق'), 'overdue')

    await waitFor(() => {
      const lastCall = workspace.mock.calls.at(-1)?.[0]
      expect(lastCall).toMatchObject({ status: 3, overdueOnly: true })
    })
  })

  it('preserves deep-linked pagination on initial load', async () => {
    renderPage('/notes/workspace?page=3&noteId=11111111-1111-1111-1111-111111111111')

    await waitFor(() => {
      expect(workspace).toHaveBeenCalledWith(expect.objectContaining({ page: 3 }))
    })

    await new Promise((resolve) => window.setTimeout(resolve, 350))

    expect(workspace.mock.calls.at(-1)?.[0]).toMatchObject({ page: 3 })
  })

  it.each([
    ['summary', 'الوصف'],
    ['evidence', 'لا توجد مرفقات'],
    ['bogus', 'الوصف'],
    [null, 'الوصف'],
  ])('resolves section=%s to a valid workspace panel', async (section, expectedText) => {
    const query = new URLSearchParams({
      noteId: '11111111-1111-1111-1111-111111111111',
    })
    if (section) {
      query.set('section', section)
    }

    renderPage(`/notes/workspace?${query.toString()}`)

    await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })
    expect(screen.getByText(expectedText)).toBeInTheDocument()
  })

  it('clears inline action state when switching selected notes', async () => {
    workspace.mockResolvedValue({ notes: { items: [note, secondNote], page: 1, pageSize: 20, totalCount: 2 } })
    workspaceDetail.mockImplementation(async (id: string) => {
      if (id === secondNote.id) {
        return { ...secondDetail, allowedActions: ['CANCEL'] }
      }

      return { ...detail, allowedActions: ['CANCEL'] }
    })

    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: /OBS-00000024/ }))
    await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })
    await userEvent.click(screen.getByRole('button', { name: 'إلغاء' }))
    await userEvent.type(screen.getByLabelText('سبب الإجراء'), 'سبب تشغيلي واضح')

    await userEvent.click(screen.getByRole('button', { name: /OBS-00000025/ }))
    await screen.findByRole('heading', { name: 'تسرب مياه في غرفة الخدمات' })

    expect(screen.queryByLabelText('سبب الإجراء')).not.toBeInTheDocument()
  })

  it('navigates to the previous/next note within the currently loaded list window', async () => {
    workspace.mockResolvedValue({ notes: { items: [note, secondNote], page: 1, pageSize: 20, totalCount: 2 } })
    workspaceDetail.mockImplementation(async (id: string) =>
      id === secondNote.id ? secondDetail : detail)

    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: /OBS-00000024/ }))
    await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })

    expect(screen.getByRole('button', { name: /السابقة/ })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: /التالية/ }))

    expect(await screen.findByRole('heading', { name: 'تسرب مياه في غرفة الخدمات' })).toBeInTheDocument()
  })

  it('restores note selection and closed detail state through browser Back and Forward navigation', async () => {
    workspace.mockResolvedValue({ notes: { items: [note, secondNote], page: 1, pageSize: 20, totalCount: 2 } })
    workspaceDetail.mockImplementation(async (id: string) =>
      id === secondNote.id ? secondDetail : detail)
    const user = userEvent.setup()
    const { router } = renderPageWithRouter()

    await user.click(await screen.findByRole('button', { name: /OBS-00000024/ }))
    expect(await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /OBS-00000025/ }))
    expect(await screen.findByRole('heading', { name: 'تسرب مياه في غرفة الخدمات' })).toBeInTheDocument()

    await router.navigate(-1)
    expect(await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })).toBeInTheDocument()

    await router.navigate(-1)
    expect(await screen.findByText('اختر ملاحظة')).toBeInTheDocument()

    await router.navigate(1)
    expect(await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })).toBeInTheDocument()

    await router.navigate(1)
    expect(await screen.findByRole('heading', { name: 'تسرب مياه في غرفة الخدمات' })).toBeInTheDocument()
  })

  it('keeps filter context when the in-page mobile back control returns to the list', async () => {
    const { router } = renderPageWithRouter('/notes/workspace?facilityId=facility-1&noteId=11111111-1111-1111-1111-111111111111&source=facility%3Afacility-1')

    await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })
    await userEvent.click(screen.getByRole('button', { name: 'رجوع إلى القائمة' }))

    await waitFor(() => {
      expect(screen.getByText('اختر ملاحظة')).toBeInTheDocument()
      expect(router.state.location.search).toContain('facilityId=facility-1')
      expect(router.state.location.search).not.toContain('noteId=')
    })
  })

  it('clears the file input and invalidates detail data after an attachment upload succeeds', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
    const user = userEvent.setup()

    renderPage('/notes/workspace?noteId=11111111-1111-1111-1111-111111111111&section=evidence', queryClient)

    await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })
    const input = screen.getByLabelText('إضافة مرفق') as HTMLInputElement
    const button = screen.getByRole('button', { name: 'رفع المرفق' })

    expect(button).toBeDisabled()
    const file = new File(['evidence'], 'evidence.txt', { type: 'text/plain' })
    await user.upload(input, file)

    expect(input.files).toHaveLength(1)
    expect(button).toBeEnabled()

    await user.click(button)

    await waitFor(() => {
      expect(uploadAttachment).toHaveBeenCalledWith(file, 'OperationalNote', note.id, 'مرفق داعم للملاحظة')
      expect(input.value).toBe('')
      expect(input.files).toHaveLength(0)
      expect(button).toBeDisabled()
      expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['notes-workspace-detail', note.id] })
    })
  })

  it('shows a dedicated closure-summary form for VERIFY_CLOSURE and a return link to the originating facility workspace', async () => {
    workspaceDetail.mockResolvedValue({ ...detail, allowedActions: ['VERIFY_CLOSURE'] })
    verifyClosure.mockResolvedValue({ ...detail.note, status: 6 })

    renderPage('/notes/workspace?noteId=11111111-1111-1111-1111-111111111111&source=facility%3Afacility-1')

    expect(await screen.findByText('← العودة إلى مساحة عمل السجن')).toBeInTheDocument()
    await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })
    await userEvent.click(screen.getByRole('button', { name: 'اعتماد الإغلاق' }))
    expect(screen.getByLabelText('ملخص الإغلاق')).toBeInTheDocument()
  })

  it('renders the Summary/Processing/Assignment/Evidence/History sections with no permanent placeholder tabs', async () => {
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: /OBS-00000024/ }))
    await screen.findByRole('heading', { name: 'تعطل إنارة الممر الرئيسي' })

    expect(screen.getByRole('button', { name: 'الملخص' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'المعالجة' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'التكليف' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'الأدلة' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'السجل الزمني' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'الموارد' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'الروابط' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'القرارات' })).not.toBeInTheDocument()
  })
})
