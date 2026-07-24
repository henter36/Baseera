import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MyFormResponsesPage } from './MyFormResponsesPage'

vi.mock('../../api/client', () => ({
  api: {
    formResponses: {
      workspace: vi.fn(),
    },
  },
}))

import { api } from '../../api/client'

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <MyFormResponsesPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('MyFormResponsesPage', () => {
  beforeEach(() => {
    vi.mocked(api.formResponses.workspace).mockReset()
  })

  it('shows empty state', async () => {
    vi.mocked(api.formResponses.workspace).mockResolvedValue({ items: [], page: 1, pageSize: 50, totalCount: 0 })
    renderPage()
    expect(await screen.findByText(/لا توجد استحقاقات/)).toBeInTheDocument()
  })

  it('renders workspace rows and RTL tabs', async () => {
    vi.mocked(api.formResponses.workspace).mockResolvedValue({
      items: [{
        assignmentId: 'a1',
        campaignId: 'c1',
        campaignCode: 'C',
        campaignNameAr: 'حملة أ',
        cycleId: 'cy1',
        occurrenceKey: '1',
        facilityId: 'f1',
        facilityNameAr: 'سجن أ',
        regionId: 'r1',
        regionNameAr: 'منطقة',
        openAtUtc: new Date().toISOString(),
        dueAtUtc: new Date().toISOString(),
        graceEndsAtUtc: new Date().toISOString(),
        closeAtUtc: new Date().toISOString(),
        effectiveDueAtUtc: new Date().toISOString(),
        responseStatus: 0,
        workStatus: 1,
        isOverdue: false,
        isCompleted: false,
        currentReviewLevel: 0,
        requiredApprovalLevels: 0,
        allowedActions: ['SaveDraft'],
      }],
      page: 1,
      pageSize: 50,
      totalCount: 1,
    })
    const { container } = renderPage()
    expect(container.querySelector('.page')).toHaveAttribute('dir', 'rtl')
    expect(screen.getByRole('tablist', { name: 'تصفية الاستحقاقات' })).toBeInTheDocument()
    expect(await screen.findByText('حملة أ')).toBeInTheDocument()
    expect(screen.getByText('مسودة')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'فتح' })).toHaveAttribute('href', '/form-assignments/a1/respond')
  })

  it('shows error state', async () => {
    vi.mocked(api.formResponses.workspace).mockRejectedValue(new Error('fail'))
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent(/تعذر تحميل/)
  })
})
