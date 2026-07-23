import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { FormCampaignsListPage } from './FormCampaignsListPage'
import { FormCampaignStatusLabelsAr, FormRecurrenceKindLabelsAr, formatCycleStatusAr } from '../../formCampaigns/campaignLabels'

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Forms.View' || code === 'Forms.ManageCampaigns',
}))

const listMock = vi.fn()

vi.mock('../../api/client', () => ({
  api: {
    formCampaigns: {
      list: (...args: unknown[]) => listMock(...args),
    },
  },
}))

function renderList() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <FormCampaignsListPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormCampaignsListPage', () => {
  it('renders Arabic campaign list with accessible search', async () => {
    listMock.mockResolvedValue({
      items: [{
        id: 'c1',
        code: 'CMP1',
        nameAr: 'حملة تجريبية',
        formDefinitionId: 'f1',
        formCode: 'F1',
        formNameAr: 'نموذج',
        formVersionId: 'v1',
        versionNumber: 1,
        status: 0,
        recurrenceKind: 0,
        firstOpenAtLocal: '2026-07-01T06:00:00Z',
        nextOccurrenceUtc: null,
        cycleCount: 0,
        allowedActions: ['edit'],
        rowVersion: 'AA==',
      }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    })
    renderList()
    expect(await screen.findByText('حملات نشر النماذج')).toBeInTheDocument()
    expect(await screen.findByText('حملة تجريبية')).toBeInTheDocument()
    expect(screen.getByLabelText('بحث')).toBeInTheDocument()
  })

  it('shows loading then empty success without treating loading as empty', async () => {
    let resolveList: (value: unknown) => void = () => undefined
    listMock.mockImplementation(() => new Promise((resolve) => { resolveList = resolve }))
    renderList()
    expect(screen.getByText('جاري التحميل…')).toBeInTheDocument()
    expect(screen.queryByText('لا توجد حملات.')).not.toBeInTheDocument()
    resolveList({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
    expect(await screen.findByText('لا توجد حملات.')).toBeInTheDocument()
  })

  it('shows error state without empty list message', async () => {
    listMock.mockRejectedValue(new Error('network'))
    renderList()
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.queryByText('لا توجد حملات.')).not.toBeInTheDocument()
  })
})

describe('campaign labels', () => {
  it('covers status and recurrence Arabic labels', () => {
    expect(FormCampaignStatusLabelsAr[0]).toBe('مسودة')
    expect(FormRecurrenceKindLabelsAr[3]).toBe('شهري')
  })

  it('formatCycleStatusAr returns unknown for unmapped status', () => {
    expect(formatCycleStatusAr(999)).toBe('حالة غير معروفة')
  })
})
