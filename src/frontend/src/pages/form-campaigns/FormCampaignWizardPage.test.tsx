import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router'
import { FormCampaignWizardPage } from './FormCampaignWizardPage'

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Forms.ManageCampaigns',
}))

vi.mock('../../api/client', () => ({
  api: {
    forms: {
      list: async () => ({
        items: [{ id: 'f1', nameAr: 'نموذج اختبار', code: 'F1' }],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      }),
      listVersions: async () => ([
        { id: 'v1', versionNumber: 1, status: 4 },
      ]),
    },
    formCampaigns: {
      targetRegions: async () => ({ items: [], page: 1, pageSize: 100, totalCount: 0 }),
      targetFacilities: async () => ({ items: [], page: 1, pageSize: 100, totalCount: 0 }),
      schedulePreview: async () => [],
      create: async () => ({ id: 'c1' }),
    },
  },
}))

function renderWizard() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <FormCampaignWizardPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormCampaignWizardPage accessibility', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('exposes accessible names for step 0 selects', async () => {
    renderWizard()
    expect(await screen.findByLabelText('النموذج')).toBeInTheDocument()
    expect(screen.getByLabelText('إصدار مقفل')).toBeInTheDocument()
  })

  it('exposes accessible names for step 1 inputs', async () => {
    const user = userEvent.setup()
    renderWizard()
    await user.click(screen.getByRole('button', { name: 'التالي' }))
    expect(await screen.findByLabelText('الرمز')).toBeInTheDocument()
    expect(screen.getByLabelText('الاسم العربي')).toBeInTheDocument()
  })

  it('keeps wizard steps and RTL panel', async () => {
    renderWizard()
    expect(await screen.findByRole('heading', { name: 'معالج إنشاء حملة نشر' })).toBeInTheDocument()
    expect(screen.getByLabelText('خطوات المعالج')).toBeInTheDocument()
    expect(document.querySelector('.panel')?.getAttribute('dir')).toBe('rtl')
  })

  it('supports keyboard navigation between steps', async () => {
    const user = userEvent.setup()
    renderWizard()
    const next = await screen.findByRole('button', { name: 'التالي' })
    next.focus()
    await user.keyboard('{Enter}')
    expect(await screen.findByLabelText('الرمز')).toBeInTheDocument()
  })
})
