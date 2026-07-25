import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { FacilityOccupancyPage } from './FacilityOccupancyPage'

const {
  currentPermissions,
  importMovements,
  recordCapacity,
  recordSnapshot,
  summary,
  units,
  movementsSummary,
} = vi.hoisted(() => ({
  currentPermissions: new Set<string>(),
  importMovements: vi.fn(),
  recordCapacity: vi.fn(),
  recordSnapshot: vi.fn(),
  summary: vi.fn(),
  units: vi.fn(),
  movementsSummary: vi.fn(),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => currentPermissions.has(code),
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      occupancy: {
        summary,
        units,
        movementsSummary,
        recordCapacity,
        recordSnapshot,
        importMovements,
      },
    },
  }
})

describe('FacilityOccupancyPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.setSystemTime(new Date('2026-07-24T10:00:00.000Z'))
    currentPermissions.clear()
    for (const permission of [
      'Occupancy.ViewSummary',
      'Occupancy.ViewUnitBreakdown',
      'Occupancy.ViewMovements',
      'Occupancy.ManageCapacity',
      'Occupancy.RecordSnapshot',
      'Occupancy.Import',
    ]) {
      currentPermissions.add(permission)
    }

    summary.mockReset()
    units.mockReset()
    movementsSummary.mockReset()
    recordCapacity.mockReset()
    recordSnapshot.mockReset()
    importMovements.mockReset()
    summary.mockResolvedValue({
      facilityId: 'facility-a',
      approvedCapacity: 100,
      currentCount: 97,
      occupancyRate: 0.97,
      availablePlaces: 3,
      overCapacityCount: 0,
      statusCode: 'high',
      statusAr: 'مرتفع',
      unitCount: 1,
      overloadedUnits: 0,
      emptyUnits: 0,
      latestSnapshotAtUtc: '2026-07-24T09:00:00.000Z',
      sourceCode: 'authoritative-snapshot',
      sourceAr: 'Snapshot رسمي',
      freshnessStatus: 'current',
      confidenceLevel: 'high',
      isPartial: false,
      warnings: [],
    })
    units.mockResolvedValue({
      units: [{
        unitId: 'unit-a',
        unitNameAr: 'عنبر أ',
        unitCode: 'A',
        approvedCapacity: 100,
        currentCount: 97,
        occupancyRate: 0.97,
        availablePlaces: 3,
        overloadCount: 0,
        statusCode: 'high',
        statusAr: 'مرتفع',
        lastUpdatedAtUtc: '2026-07-24T09:00:00.000Z',
        dataSourceAr: 'Snapshot إشغال',
        openNotesCount: 0,
        openIncidentsCount: 0,
        riskCount: 0,
        alertReasons: [],
      }],
    })
    movementsSummary.mockResolvedValue({
      admissions: 1,
      releases: 0,
      transferIn: 0,
      transferOut: 0,
      internalTransfers: 0,
      temporaryLeave: 0,
      returns: 0,
      death: 0,
      hospitalTransfers: 0,
      courtTransfers: 0,
      corrections: 0,
      otherMovements: 0,
      netMovement: 1,
      dailyTrend: [],
    })
    recordCapacity.mockResolvedValue({ id: 'capacity-a' })
    recordSnapshot.mockResolvedValue({ id: 'snapshot-a' })
    importMovements.mockResolvedValue({ acceptedRows: 1, duplicateRows: 0, rejectedRows: [] })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('renders unit rows as non-interactive content with accessible occupancy percentage', async () => {
    renderPage()

    const list = await screen.findByRole('list', { name: 'وحدات الإشغال' })
    expect(within(list).queryByRole('button')).not.toBeInTheDocument()
    expect(screen.getByLabelText('نسبة إشغال وحدة عنبر أ: 97%')).toBeInTheDocument()
  })

  it('uses fresh query times when mutations invalidate occupancy data', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    renderPage()
    await waitFor(() => expect(summary).toHaveBeenCalled())
    expect(summary).toHaveBeenLastCalledWith('facility-a', { asOfUtc: '2026-07-24T10:00:00.000Z' })

    vi.setSystemTime(new Date('2026-07-24T10:05:00.000Z'))
    await user.type(screen.getAllByLabelText('العدد')[0], '120')
    await user.type(screen.getByLabelText('يسري من'), '2026-07-24T14:30')
    await user.type(screen.getAllByLabelText('مرجع المصدر')[0], 'CAP-1')
    await user.click(screen.getByRole('button', { name: 'حفظ الطاقة' }))

    await waitFor(() => {
      const lastCall = summary.mock.calls.at(-1)
      expect(lastCall?.[0]).toBe('facility-a')
      expect(Date.parse(lastCall?.[1].asOfUtc)).toBeGreaterThan(Date.parse('2026-07-24T10:00:00.000Z'))
    })
    expect(recordCapacity).toHaveBeenCalledWith('facility-a', expect.objectContaining({
      effectiveFromUtc: '2026-07-24T11:30:00.000Z',
    }))
  })

  it('shows backend mutation errors and keeps validation detail visible', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    recordSnapshot.mockRejectedValueOnce(new ApiError(400, 'وقت الالتقاط مطلوب.'))
    renderPage()
    await waitFor(() => expect(summary).toHaveBeenCalled())

    await user.type(screen.getByLabelText('وقت الالتقاط'), '2026-07-24T14:30')
    await user.type(screen.getAllByLabelText('العدد')[1], '97')
    await user.type(screen.getAllByLabelText('مرجع المصدر')[1], 'SNAP-1')
    await user.click(screen.getByRole('button', { name: 'حفظ Snapshot' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('وقت الالتقاط مطلوب.')
  })

  it('maps movement imports with typed enum values and Riyadh UTC conversion', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    renderPage()
    await waitFor(() => expect(summary).toHaveBeenCalled())

    await user.type(screen.getByLabelText('نظام المصدر'), 'inmate-system')
    await user.type(screen.getByLabelText('مرجع الاستيراد'), 'batch-1')
    await user.type(screen.getByLabelText('Hash النزيل'), 'hash-1')
    await user.type(screen.getByLabelText('إلى سجن'), 'facility-a')
    await user.type(screen.getByLabelText('وقت الحركة'), '2026-07-24T14:30')
    await user.type(screen.getByLabelText('معرف الحدث الخارجي'), 'event-1')
    await user.click(screen.getByRole('button', { name: 'استيراد الحركة' }))

    expect(importMovements).toHaveBeenCalledWith('facility-a', expect.objectContaining({
      rows: [expect.objectContaining({
        movementType: 0,
        occurredAtUtc: '2026-07-24T11:30:00.000Z',
      })],
    }))
  })
})

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/facilities/facility-a/occupancy']}>
        <Routes>
          <Route path="/facilities/:facilityId/occupancy" element={<FacilityOccupancyPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}
