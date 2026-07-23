import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { formatCycleStatusAr } from '../../formCampaigns/campaignLabels'
import { FormCampaignCycleDetailPage, FormCampaignCyclesPage } from './FormCampaignCyclesPage'

const { cycles, cycle, assignments, permissions } = vi.hoisted(() => ({
  cycles: vi.fn(),
  cycle: vi.fn(),
  assignments: vi.fn(),
  permissions: new Set<string>(['Forms.View', 'Forms.ViewCampaignAssignments']),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => permissions.has(code),
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      formCampaigns: {
        cycles,
        cycle,
        assignments,
      },
    },
  }
})

const baseCycle = {
  id: 'cycle-1',
  sequenceNumber: 1,
  occurrenceKey: 'occ-1',
  status: 1,
  scheduledOccurrenceLocal: '2026-07-01T06:00:00Z',
  openAtUtc: '2026-07-01T06:00:00Z',
  dueAtUtc: '2026-07-02T06:00:00Z',
  closeAtUtc: '2026-07-02T07:00:00Z',
  assignedFacilityCount: 3,
  targetSnapshotHash: 'abcdef1234567890',
}

function renderCyclesPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/form-campaigns/c1/cycles']}>
        <Routes>
          <Route path="/form-campaigns/:campaignId/cycles" element={<FormCampaignCyclesPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function renderCycleDetailPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/form-campaigns/c1/cycles/cycle-1']}>
        <Routes>
          <Route path="/form-campaigns/:campaignId/cycles/:cycleId" element={<FormCampaignCycleDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormCampaignCyclesPage', () => {
  beforeEach(() => {
    cycles.mockReset()
    permissions.clear()
    permissions.add('Forms.View')
  })

  it('shows الإجراءات header and assignments link aria-label', async () => {
    cycles.mockResolvedValue({ items: [baseCycle], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 })
    renderCyclesPage()
    expect(await screen.findByText('الإجراءات')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'عرض تعيينات الدورة 1' })).toBeInTheDocument()
  })
})

describe('FormCampaignCycleDetailPage assignments section', () => {
  beforeEach(() => {
    cycle.mockReset()
    assignments.mockReset()
    permissions.clear()
    permissions.add('Forms.View')
    permissions.add('Forms.ViewCampaignAssignments')
    cycle.mockResolvedValue({
      ...baseCycle,
      campaignId: 'c1',
      scheduledOccurrenceUtc: '2026-07-01T06:00:00Z',
      schemaHash: 'schema',
      generatedAtUtc: '2026-07-01T06:00:00Z',
    })
  })

  it('shows loading state for assignments', async () => {
    assignments.mockReturnValue(new Promise(() => {}))
    renderCycleDetailPage()
    expect(await screen.findByText('جاري تحميل التعيينات…')).toBeInTheDocument()
  })

  it('shows error state for assignments', async () => {
    assignments.mockRejectedValue(new ApiError(403, 'forbidden'))
    renderCycleDetailPage()
    expect(await screen.findByText('ليست لديك صلاحية.')).toBeInTheDocument()
  })

  it('shows empty state for assignments', async () => {
    assignments.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
    renderCycleDetailPage()
    expect(await screen.findByText('لا توجد تعيينات.')).toBeInTheDocument()
  })

  it('renders assignments table on success', async () => {
    assignments.mockResolvedValue({
      items: [{
        id: 'a1',
        facilityCodeAtAssignment: 'A1',
        facilityNameArAtAssignment: 'موقع',
        regionNameArAtAssignment: 'منطقة',
        facilityTypeAtAssignment: 'سجن',
        isAvailable: true,
      }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    })
    renderCycleDetailPage()
    expect(await screen.findByText('A1')).toBeInTheDocument()
    expect(screen.getByText('موقع')).toBeInTheDocument()
  })
})

describe('formatCycleStatusAr', () => {
  it('returns unknown label for unmapped status', () => {
    expect(formatCycleStatusAr(999)).toBe('حالة غير معروفة')
  })
})
