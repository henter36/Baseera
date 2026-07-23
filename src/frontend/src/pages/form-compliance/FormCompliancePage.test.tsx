import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { FormCompliancePage } from './FormCompliancePage'

const {
  summary,
  regions,
  facilities,
  cycles,
  pending,
  trend,
  exportCsv,
  listRegions,
  listFacilities,
  canExport,
} = vi.hoisted(() => ({
  summary: vi.fn(),
  regions: vi.fn(),
  facilities: vi.fn(),
  cycles: vi.fn(),
  pending: vi.fn(),
  trend: vi.fn(),
  exportCsv: vi.fn(),
  listRegions: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  listFacilities: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  canExport: { value: false },
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => {
    if (code === 'Forms.ViewComplianceDashboard') return true
    if (code === 'Forms.ExportComplianceDashboard') return canExport.value
    return false
  },
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      regions: listRegions,
      facilities: listFacilities,
      formCompliance: {
        summary,
        regions,
        facilities,
        cycles,
        pending,
        trend,
        exportCsv,
      },
    },
  }
})

function renderPage(route = '/form-compliance') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path="/form-compliance" element={<FormCompliancePage />} />
          <Route path="/form-compliance/regions/:regionId" element={<FormCompliancePage />} />
          <Route path="/form-compliance/facilities/:facilityId" element={<FormCompliancePage />} />
          <Route path="/form-compliance/cycles/:cycleId" element={<FormCompliancePage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormCompliancePage', () => {
  beforeEach(() => {
    canExport.value = false
    Object.defineProperty(URL, 'createObjectURL', { value: vi.fn(() => 'blob:csv'), configurable: true })
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true })
    listRegions.mockResolvedValue({ items: [{ id: 'r1', code: 'R1', nameAr: 'منطقة الاختبار' }], page: 1, pageSize: 50, totalCount: 1 })
    listFacilities.mockResolvedValue({ items: [], page: 1, pageSize: 50, totalCount: 0 })
    summary.mockResolvedValue({
      targetedAssignmentCount: 2,
      distinctFacilityCount: 1,
      unavailableAssignmentCount: 1,
      eligibleAssignmentCount: 0,
      completedCount: 0,
      remainingCount: 0,
      completionRate: null,
      notStartedCount: 0,
      draftCount: 0,
      submittedCount: 0,
      underReviewCount: 0,
      returnedCount: 0,
      approvedCount: 0,
      rejectedCount: 0,
      closedCount: 0,
      overdueCount: 0,
      completedOnTimeCount: 0,
      completedLateCount: 0,
      averageCompletionMinutes: null,
      unknownCompletionTimestampCount: 0,
      invalidCompletionDurationCount: 0,
      statusBucketTotal: 0,
      statusReconciliationValid: true,
      generatedAtUtc: '2026-07-23T10:00:00Z',
    })
    regions.mockResolvedValue({
      items: [{
        regionIdAtAssignment: 'r1',
        regionNameAtAssignment: 'منطقة الاختبار',
        targetedAssignmentCount: 3,
        unavailableAssignmentCount: 1,
        eligibleAssignmentCount: 2,
        completedCount: 1,
        remainingCount: 1,
        completionRate: 50,
        overdueCount: 1,
        notStartedCount: 1,
        returnedCount: 0,
        averageCompletionMinutes: 60,
        rank: 1,
      }],
      page: 1,
      pageSize: 20,
      totalCount: 45,
    })
    facilities.mockResolvedValue({
      items: [{
        facilityId: 'f1',
        facilityCodeAtAssignment: 'F-1',
        facilityNameAtAssignment: 'سجن الاختبار',
        regionIdAtAssignment: 'r1',
        regionNameAtAssignment: 'منطقة الاختبار',
        cycleCount: 1,
        eligibleAssignmentCount: 1,
        completedCount: 0,
        remainingCount: 1,
        completionRate: 0,
        overdueCount: 1,
        latestEffectiveDueAtUtc: '2026-07-23T08:00:00Z',
        responsibleUserId: null,
        responsibleUserName: null,
        allowedActions: ['open-response'],
      }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    })
    cycles.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 25 })
    pending.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    trend.mockResolvedValue([])
    exportCsv.mockResolvedValue({ blob: new Blob(['x']), fileName: 'x.csv' })
  })

  afterEach(() => {
    vi.clearAllMocks()
    vi.useRealTimers()
  })

  it('renders summary cards and uses dash for zero denominator', async () => {
    renderPage()

    expect(await screen.findByText('المواقع المؤهلة')).toBeInTheDocument()
    expect(screen.getByText('نسبة الالتزام')).toBeInTheDocument()
    expect(screen.getAllByText('—').length).toBeGreaterThan(0)
  })

  it('renders responsible user null as unspecified', async () => {
    renderPage()

    expect(await screen.findByText('سجن الاختبار')).toBeInTheDocument()
    expect(screen.getByText('غير محدد')).toBeInTheDocument()
  })

  it('hides export action without export permission', async () => {
    renderPage()

    await screen.findByText('لوحة التزام النماذج')
    expect(screen.queryByRole('button', { name: 'تصدير CSV' })).not.toBeInTheDocument()
  })

  it('updates URL filters from search input', async () => {
    renderPage()

    const search = await screen.findByLabelText('بحث')
    fireEvent.change(search, { target: { value: 'اختبار' } })
    expect(summary).not.toHaveBeenLastCalledWith(expect.objectContaining({ search: expect.stringContaining('اختبار') }))
    await waitFor(() => {
      expect(summary).toHaveBeenLastCalledWith(expect.objectContaining({ search: expect.stringContaining('اختبار') }))
    })
  })

  it('passes independent pagination to regions without changing facilities page', async () => {
    const user = userEvent.setup()
    renderPage()

    await screen.findByText('منطقة الاختبار')
    await user.click(screen.getAllByRole('button', { name: 'التالي' })[0])

    await waitFor(() => {
      expect(regions).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2, pageSize: 20 }))
    })
    expect(facilities).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1, pageSize: 20 }))
    expect(screen.getByText(/عرض ٢١–٤٠ من إجمالي ٤٥/)).toBeInTheDocument()
  })

  it('changes page size for a single table and resets that table page', async () => {
    const user = userEvent.setup()
    renderPage()

    await screen.findByText('منطقة الاختبار')
    await user.selectOptions(screen.getByLabelText('عدد صفوف المناطق'), '50')

    await waitFor(() => {
      expect(regions).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1, pageSize: 50 }))
    })
  })

  it('exports each compliance section with the matching view', async () => {
    canExport.value = true
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'تصدير المناطق' }))
    await user.click(screen.getByRole('button', { name: 'تصدير المواقع' }))
    await user.click(screen.getByRole('button', { name: 'تصدير الدورات' }))
    await user.click(screen.getByRole('button', { name: 'تصدير المتبقي' }))

    expect(exportCsv).toHaveBeenCalledWith(expect.objectContaining({ view: 0 }))
    expect(exportCsv).toHaveBeenCalledWith(expect.objectContaining({ view: 1 }))
    expect(exportCsv).toHaveBeenCalledWith(expect.objectContaining({ view: 2 }))
    expect(exportCsv).toHaveBeenCalledWith(expect.objectContaining({ view: 3 }))
  })

  it('keeps explicit route clear from restoring region default', async () => {
    const user = userEvent.setup()
    renderPage('/form-compliance/regions/r1')

    await screen.findByText('منطقة الاختبار')
    expect(screen.getByLabelText('المنطقة')).toHaveValue('r1')
    await user.selectOptions(screen.getByLabelText('المنطقة'), '')

    await waitFor(() => {
      expect(summary).toHaveBeenLastCalledWith(expect.not.objectContaining({ regionId: 'r1' }))
    })
    expect(screen.getByLabelText('المنطقة')).toHaveValue('')
  })
})
