import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { CorrectiveActionsListPage } from './CorrectiveActionsListPage'

const { listActions, listRegions, listFacilities } = vi.hoisted(() => ({
  listActions: vi.fn(),
  listRegions: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  listFacilities: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'CorrectiveActions.View',
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      regions: listRegions,
      facilities: listFacilities,
      correctiveActions: { ...actual.api.correctiveActions, list: listActions },
    },
  }
})

const sampleAction = {
  id: 'ca-1',
  referenceNumber: 'CA-00000001',
  operationalNoteId: 'note-1',
  operationalNoteReferenceNumber: 'OBS-00000001',
  title: 'إصلاح بوابة',
  descriptionSnippet: 'تم إصلاح الخلل',
  priority: 3,
  priorityAr: 'حرجة',
  status: 4,
  statusAr: 'بانتظار التحقق',
  classification: 0,
  ownerDepartmentId: null,
  dueAtUtc: '2020-01-01T00:00:00Z',
  isOverdue: true,
  isDueSoon: false,
  overdueDays: 2,
  currentAssigneeDisplay: 'فريق التشغيل',
  createdAtUtc: '2024-01-01T00:00:00Z',
  rowVersion: 'row-v1',
  isSensitiveRedacted: false,
}

function renderPage(path = '/corrective-actions') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <CorrectiveActionsListPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('CorrectiveActionsListPage', () => {
  beforeEach(() => {
    listActions.mockReset()
    listRegions.mockClear()
    listFacilities.mockClear()
  })

  it('shows loading and empty states', async () => {
    let resolve!: (value: unknown) => void
    listActions.mockImplementation(() => new Promise((r) => { resolve = r }))
    renderPage()
    expect(screen.getByText('جاري التحميل…')).toBeInTheDocument()

    resolve({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    expect(await screen.findByText('لا توجد إجراءات تصحيحية مطابقة ضمن نطاقك.')).toBeInTheDocument()
  })

  it('shows an API failure and retries', async () => {
    listActions.mockRejectedValueOnce(new ApiError(500, 'تعذر تحميل الإجراءات.'))
    listActions.mockResolvedValueOnce({ items: [sampleAction], page: 1, pageSize: 20, totalCount: 1 })
    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent('تعذر تحميل الإجراءات.')
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'إعادة المحاولة' }))

    expect(await screen.findByText('إصلاح بوابة')).toBeInTheDocument()
    expect(listActions).toHaveBeenCalledTimes(2)
  })

  it('renders badges, linked note, assignee, overdue days, and detail link', async () => {
    listActions.mockResolvedValue({ items: [sampleAction], page: 1, pageSize: 20, totalCount: 1 })
    renderPage()

    expect(await screen.findByText('CA-00000001')).toBeInTheDocument()
    const row = screen.getByText('CA-00000001').closest('tr')!
    expect(within(row).getByText('بانتظار التحقق')).toBeInTheDocument()
    expect(within(row).getByText('حرجة')).toBeInTheDocument()
    expect(within(row).getByText('2 يوم')).toBeInTheDocument()
    expect(within(row).getByText('OBS-00000001')).toBeInTheDocument()
    expect(within(row).getByText('فريق التشغيل')).toBeInTheDocument()
    expect(within(row).getByText('عرض')).toHaveAttribute('href', '/corrective-actions/ca-1')
  })

  it('sends filters and noteId query to the API', async () => {
    listActions.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    renderPage('/corrective-actions?noteId=note-1')
    await waitFor(() => expect(listActions).toHaveBeenCalled())

    const user = userEvent.setup()
    await user.type(screen.getByLabelText('بحث الإجراءات'), 'CA-1')
    await user.selectOptions(screen.getByLabelText('حالة الإجراء'), '4')
    await user.selectOptions(screen.getByLabelText('أولوية الإجراء'), '3')
    await user.click(screen.getByLabelText('المتأخرة فقط'))

    await waitFor(() => {
      const lastCall = listActions.mock.calls.at(-1)?.[0]
      expect(lastCall).toMatchObject({ noteId: 'note-1', search: 'CA-1', status: 4, priority: 3, overdueOnly: true })
    })
  })

  it('paginates and toggles stable server sort', async () => {
    listActions.mockResolvedValue({ items: [sampleAction], page: 1, pageSize: 20, totalCount: 41 })
    renderPage()
    await screen.findByText('إصلاح بوابة')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'التالي' }))
    await waitFor(() => expect(listActions.mock.calls.at(-1)?.[0]).toMatchObject({ page: 2 }))

    await user.click(within(screen.getByText('الرقم المرجعي').closest('th')!).getByRole('button'))
    await waitFor(() => expect(listActions.mock.calls.at(-1)?.[0]).toMatchObject({ sortBy: 'referenceNumber', sortDesc: true }))
  })
})
