import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { dashboardErrorMessage, metricCardClassName, OperationalDashboardPage } from './OperationalDashboardPage'

const {
  summary,
  trends,
  breakdowns,
  priorityQueues,
  listRegions,
  listFacilities,
  myNoteTypes,
} = vi.hoisted(() => ({
  summary: vi.fn(),
  trends: vi.fn(),
  breakdowns: vi.fn(),
  priorityQueues: vi.fn(),
  listRegions: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  listFacilities: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  myNoteTypes: vi.fn(async () => []),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => [
    'Dashboard.ViewOperational',
    'Dashboard.ViewRisk',
    'Dashboard.ViewRouting',
    'Dashboard.ViewCorrectiveActions',
  ].includes(code),
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
      dashboard: {
        operations: {
          summary,
          trends,
          breakdowns,
          priorityQueues,
        },
      },
    },
  }
})

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <OperationalDashboardPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('OperationalDashboardPage', () => {
  beforeEach(() => {
    summary.mockResolvedValue({
      workload: {
        openTotal: 12,
        assigned: 3,
        inProgress: 2,
        pendingVerification: 1,
        reopened: 1,
        unassigned: 4,
        requiresRouting: 2,
      },
      risk: {
        overdue: 5,
        dueSoon: 2,
        criticalOrHigh: 3,
        overdueUnassigned: 1,
        activeEscalations: 0,
        routingFailureNoRule: 1,
        routingFailureNoEligibleUser: 0,
        routingFailureInvalidTarget: 0,
      },
      correctiveActions: {
        active: 4,
        overdue: 1,
        pendingVerification: 1,
        reopened: 0,
        notesWithStalledActions: 1,
      },
      routing: {
        requiresRouting: 2,
        failureNoRule: 1,
        failureNoEligibleUser: 0,
        failureInvalidTarget: 0,
      },
      fromUtc: '2024-01-01T00:00:00Z',
      toUtc: '2024-01-31T00:00:00Z',
      dueSoonDays: 7,
    })
    trends.mockResolvedValue({ points: [], fromUtc: '2024-01-01T00:00:00Z', toUtc: '2024-01-31T00:00:00Z', granularity: 'daily' })
    breakdowns.mockResolvedValue({ dimension: 1, rows: [] })
    priorityQueues.mockResolvedValue({ limit: 10, mostOverdueNotes: [], criticalUnassignedNotes: [], topOverdueLocations: [], mostOverdueCorrectiveActions: [], recentRoutingFailures: [] })
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('renders KPI cards from summary API', async () => {
    renderPage()
    expect(await screen.findByText('12')).toBeInTheDocument()
    expect(screen.getByText('إجمالي المفتوحة')).toBeInTheDocument()
    expect(screen.getByText('5')).toBeInTheDocument()
  })

  it('passes period filter to summary API', async () => {
    renderPage()
    await screen.findByText('12')
    expect(summary).toHaveBeenCalledWith(expect.objectContaining({ periodDays: 30 }))
  })

  it('updates API filters when period changes', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('12')
    await user.selectOptions(screen.getByLabelText('الفترة'), '7')
    await waitFor(() => {
      expect(summary).toHaveBeenCalledWith(expect.objectContaining({ periodDays: 7 }))
    })
  })

  it('shows drill-down links for overdue KPI', async () => {
    renderPage()
    const link = await screen.findByRole('link', { name: /متأخرة: 5/i })
    expect(link).toHaveAttribute('href', expect.stringContaining('overdueOnly=true'))
  })

  it('shows loading state', () => {
    summary.mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText('جاري تحميل لوحة المتابعة…')).toBeInTheDocument()
  })

  it('shows error state with retry', async () => {
    summary.mockRejectedValue(new ApiError(500, 'خطأ'))
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent('خطأ')
    expect(screen.getByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument()
  })

  it('shows empty priority queue message', async () => {
    renderPage()
    expect(await screen.findByText('لا توجد عناصر في قوائم الأولوية للفلاتر الحالية.')).toBeInTheDocument()
  })

  it('builds metric card class names with and without tone', () => {
    expect(metricCardClassName()).toBe('metric-card')
    expect(metricCardClassName('danger')).toBe('metric-card metric-card--danger')
  })

  it('maps dashboard errors to readable messages', () => {
    expect(dashboardErrorMessage(new ApiError(500, 'خطأ'))).toBe('خطأ')
    expect(dashboardErrorMessage(new Error('boom'))).toBe('تعذر تحميل لوحة المتابعة.')
    expect(dashboardErrorMessage(null)).toBeNull()
  })

  it('associates filter labels with select controls', async () => {
    renderPage()
    await screen.findByText('12')

    for (const label of ['الفترة', 'المنطقة', 'الموقع', 'نوع الملاحظة', 'الخطورة', 'الحالة', 'التقسيم']) {
      const select = screen.getByLabelText(label)
      expect(select.tagName).toBe('SELECT')
      expect(select.closest('label')?.querySelector('span')?.textContent).toBe(label)
    }
  })
})
