import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { NotesListPage } from './NotesListPage'

const { listNotes, listRegions, listFacilities, myNoteTypes } = vi.hoisted(() => ({
  listNotes: vi.fn(),
  listRegions: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  listFacilities: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  myNoteTypes: vi.fn(async () => [{
    id: '44444444-4444-4444-4444-444444444403',
    code: 'OPERATIONAL',
    nameAr: 'تشغيلية',
    descriptionAr: 'ملاحظات تشغيلية',
    entryInstructionsAr: null,
    sortOrder: 30,
    isActive: true,
    defaultSeverity: 1,
    defaultSeverityAr: 'متوسطة',
    defaultDueDays: 5,
    rowVersion: 'rv',
  }]),
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
      myNoteTypes,
      notes: { ...actual.api.notes, list: listNotes },
    },
  }
})

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <NotesListPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const sampleNote = {
  id: 'note-1',
  referenceNumber: 'OBS-00000001',
  title: 'ملاحظة تجريبية',
  descriptionSnippet: 'مقتطف',
  status: 1,
  statusAr: 'مفتوحة',
  severity: 2,
  severityAr: 'عالية',
  noteTypeId: '44444444-4444-4444-4444-444444444403',
  noteTypeCode: 'OPERATIONAL',
  noteTypeNameAr: 'تشغيلية',
  noteTypeIsActive: true,
  classification: 0,
  scopeType: 3,
  regionId: null,
  facilityId: 'fac-1',
  facilityUnitId: null,
  dueAtUtc: '2020-01-01T00:00:00Z',
  isOverdue: true,
  currentAssigneeDisplay: 'محمد أحمد',
  createdAtUtc: '2024-01-01T00:00:00Z',
  rowVersion: 'abc123',
  isSensitiveRedacted: false,
}

describe('NotesListPage', () => {
  beforeEach(() => {
    listNotes.mockReset()
    listRegions.mockClear()
    listFacilities.mockClear()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('shows the empty state when there are no notes', async () => {
    listNotes.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    renderPage()

    expect(await screen.findByText('لا توجد ملاحظات مطابقة ضمن نطاقك.')).toBeInTheDocument()
  })

  it('shows a loading indicator while the request is in flight', async () => {
    let resolve!: (value: unknown) => void
    listNotes.mockImplementation(() => new Promise((r) => { resolve = r }))
    renderPage()

    expect(screen.getByText('جاري التحميل…')).toBeInTheDocument()
    resolve({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    await waitFor(() => expect(screen.queryByText('جاري التحميل…')).not.toBeInTheDocument())
  })

  it('shows an error with a retry action on API failure, and retry re-fetches', async () => {
    listNotes.mockRejectedValueOnce(new ApiError(500, 'تعذر تحميل الملاحظات.'))
    listNotes.mockResolvedValueOnce({ items: [sampleNote], page: 1, pageSize: 20, totalCount: 1 })
    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent('تعذر تحميل الملاحظات.')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'إعادة المحاولة' }))

    expect(await screen.findByText('ملاحظة تجريبية')).toBeInTheDocument()
    expect(listNotes).toHaveBeenCalledTimes(2)
  })

  it('renders badges, due date, overdue flag and assignee for list rows', async () => {
    listNotes.mockResolvedValue({ items: [sampleNote], page: 1, pageSize: 20, totalCount: 1 })
    renderPage()

    expect(await screen.findByText('OBS-00000001')).toBeInTheDocument()
    const row = screen.getByText('OBS-00000001').closest('tr')!
    expect(within(row).getByText('مفتوحة')).toBeInTheDocument()
    expect(within(row).getByText('عالية')).toBeInTheDocument()
    expect(within(row).getByText('متأخرة')).toBeInTheDocument()
    expect(within(row).getByText('محمد أحمد')).toBeInTheDocument()
  })

  it('sends selected filters to the API', async () => {
    listNotes.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    renderPage()
    await waitFor(() => expect(listNotes).toHaveBeenCalled())

    const user = userEvent.setup()
    await user.type(screen.getByLabelText('بحث الملاحظات'), 'OBS-1')
    await user.selectOptions(screen.getByLabelText('الحالة'), '1')
    await user.click(screen.getByLabelText('المتأخرة فقط'))

    await waitFor(() => {
      const lastCall = listNotes.mock.calls.at(-1)?.[0]
      expect(lastCall).toMatchObject({ search: 'OBS-1', status: 1, overdueOnly: true })
    })
  })

  it('sends the classification filter to the API (server-side, not client-side)', async () => {
    listNotes.mockResolvedValue({ items: [sampleNote], page: 1, pageSize: 20, totalCount: 1 })
    renderPage()
    await screen.findByText('OBS-00000001')

    const user = userEvent.setup()
    await user.selectOptions(screen.getByLabelText('مستوى التصنيف الأمني'), '3')

    await waitFor(() => {
      const lastCall = listNotes.mock.calls.at(-1)?.[0]
      expect(lastCall).toMatchObject({ classification: 3 })
    })
  })

  it('sends the requires routing tab filter to the API', async () => {
    listNotes.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    renderPage()
    await waitFor(() => expect(listNotes).toHaveBeenCalled())

    const user = userEvent.setup()
    await user.click(await screen.findByRole('tab', { name: 'تتطلب توجيهًا' }))

    await waitFor(() => {
      const lastCall = listNotes.mock.calls.at(-1)?.[0]
      expect(lastCall).toMatchObject({ requiresRouting: true })
    })
  })

  it('paginates using next/previous controls', async () => {
    listNotes.mockResolvedValue({ items: [sampleNote], page: 1, pageSize: 20, totalCount: 45 })
    renderPage()

    await screen.findByText('ملاحظة تجريبية')
    expect(screen.getByText(/صفحة 1 من 3/)).toBeInTheDocument()

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'التالي' }))

    await waitFor(() => {
      const lastCall = listNotes.mock.calls.at(-1)?.[0]
      expect(lastCall).toMatchObject({ page: 2 })
    })
  })

  it('toggles sort direction when clicking a column header twice', async () => {
    listNotes.mockResolvedValue({ items: [sampleNote], page: 1, pageSize: 20, totalCount: 1 })
    renderPage()
    await screen.findByText('ملاحظة تجريبية')

    const user = userEvent.setup()
    const header = within(screen.getByText('الرقم المرجعي').closest('th')!).getByRole('button')
    await user.click(header)

    await waitFor(() => {
      const lastCall = listNotes.mock.calls.at(-1)?.[0]
      expect(lastCall).toMatchObject({ sortBy: 'referenceNumber', sortDesc: true })
    })

    await user.click(header)
    await waitFor(() => {
      const lastCall = listNotes.mock.calls.at(-1)?.[0]
      expect(lastCall).toMatchObject({ sortBy: 'referenceNumber', sortDesc: false })
    })
  })

})
