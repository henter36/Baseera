import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { FormCampaignsListPage } from './FormCampaignsListPage'
import { FormCampaignStatusLabelsAr, FormRecurrenceKindLabelsAr, formatCycleStatusAr } from '../../formCampaigns/campaignLabels'

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Forms.View' || code === 'Forms.ManageCampaigns',
}))

vi.mock('../../api/client', () => ({
  api: {
    formCampaigns: {
      list: async () => ({
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
      }),
    },
  },
}))

describe('FormCampaignsListPage', () => {
  it('renders Arabic campaign list', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter>
          <FormCampaignsListPage />
        </MemoryRouter>
      </QueryClientProvider>,
    )
    expect(await screen.findByText('حملات نشر النماذج')).toBeInTheDocument()
    expect(await screen.findByText('حملة تجريبية')).toBeInTheDocument()
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
