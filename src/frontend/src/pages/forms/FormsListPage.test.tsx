import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { FormsListPage } from './FormsListPage'

const { listForms, listRegions, listFacilities } = vi.hoisted(() => ({
  listForms: vi.fn(),
  listRegions: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  listFacilities: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Forms.View' || code === 'Forms.Create',
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      regions: listRegions,
      facilities: listFacilities,
      forms: { ...actual.api.forms, list: listForms },
    },
  }
})

const sampleForm = {
  id: 'form-1',
  code: 'INCIDENT.REPORT',
  nameAr: 'نموذج واقعة',
  nameEn: null,
  descriptionSnippet: 'مقتطف',
  status: 0,
  statusAr: 'مسودة',
  classification: 0,
  scopeType: 3,
  regionId: 'region-1',
  facilityId: 'fac-1',
  facilityUnitId: null,
  ownerDepartmentId: null,
  createdAtUtc: '2024-01-01T00:00:00Z',
  rowVersion: 'rv',
  isSensitiveRedacted: false,
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <FormsListPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormsListPage', () => {
  beforeEach(() => {
    listForms.mockReset()
    listRegions.mockClear()
    listFacilities.mockClear()
  })

  afterEach(() => vi.clearAllMocks())

  it('shows the empty state when there are no forms', async () => {
    listForms.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    renderPage()
    expect(await screen.findByText('لا توجد نماذج مطابقة ضمن نطاقك.')).toBeInTheDocument()
  })

  it('shows a loading indicator while the request is in flight', async () => {
    let resolve!: (value: unknown) => void
    listForms.mockImplementation(() => new Promise((r) => { resolve = r }))
    renderPage()
    expect(screen.getByText('جاري التحميل…')).toBeInTheDocument()
    resolve({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    await waitFor(() => expect(screen.queryByText('جاري التحميل…')).not.toBeInTheDocument())
  })

  it('shows an error with retry on API failure', async () => {
    listForms.mockRejectedValueOnce(new ApiError(500, 'تعذر تحميل النماذج.'))
    listForms.mockResolvedValueOnce({ items: [sampleForm], page: 1, pageSize: 20, totalCount: 1 })
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent('تعذر تحميل النماذج.')
    await userEvent.setup().click(screen.getByRole('button', { name: 'إعادة المحاولة' }))
    expect(await screen.findByText('INCIDENT.REPORT')).toBeInTheDocument()
  })

  it('renders list rows with status badge', async () => {
    listForms.mockResolvedValue({ items: [sampleForm], page: 1, pageSize: 20, totalCount: 1 })
    renderPage()
    expect(await screen.findByText('INCIDENT.REPORT')).toBeInTheDocument()
    const row = screen.getByText('INCIDENT.REPORT').closest('tr')!
    expect(within(row).getByText('مسودة')).toBeInTheDocument()
    expect(within(row).getByText('نموذج واقعة')).toBeInTheDocument()
  })

  it('sends selected filters to the API', async () => {
    listForms.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    renderPage()
    await waitFor(() => expect(listForms).toHaveBeenCalled())
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('بحث النماذج'), 'INC')
    await user.selectOptions(screen.getByLabelText('الحالة'), '0')
    await waitFor(() => {
      const lastCall = listForms.mock.calls.at(-1)?.[0]
      expect(lastCall).toMatchObject({ search: 'INC', status: 0 })
    })
  })
})
