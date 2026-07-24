import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { FormsGovernanceSettingsPage } from './FormsGovernanceSettingsPage'

const { getPolicy, updatePolicy } = vi.hoisted(() => ({
  getPolicy: vi.fn(),
  updatePolicy: vi.fn(),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Forms.ManageGovernance',
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      formGovernance: { getPolicy, updatePolicy },
    },
  }
})

const samplePolicy = {
  id: 'policy-1',
  requireReviewBeforeApproval: true,
  requireSeparationOfDuties: true,
  allowDesignerToReviewOwnForm: false,
  allowReviewerToApproveOwnReview: false,
  allowApproverToPublish: true,
  defaultRetentionDays: 365,
  sensitiveRetentionDays: 730,
  minimumRetentionDays: 30,
  auditSensitiveViews: true,
  auditExports: true,
  requireReasonForArchive: true,
  rowVersion: 'rv-1',
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <FormsGovernanceSettingsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormsGovernanceSettingsPage', () => {
  beforeEach(() => {
    getPolicy.mockReset()
    updatePolicy.mockReset()
    getPolicy.mockResolvedValue(samplePolicy)
  })

  it('loads and displays policy values', async () => {
    renderPage()
    expect(await screen.findByLabelText('مدة الاحتفاظ الافتراضية')).toHaveValue(365)
    expect(screen.getByLabelText('يتطلب مراجعة قبل الاعتماد')).toBeChecked()
  })

  it('shows loading state', () => {
    getPolicy.mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText('جاري التحميل…')).toBeInTheDocument()
  })

  it('shows error with retry on load failure', async () => {
    getPolicy.mockRejectedValue(new ApiError(500, 'تعذر التحميل'))
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent('تعذر التحميل')
    getPolicy.mockResolvedValue(samplePolicy)
    await userEvent.setup().click(screen.getByRole('button', { name: 'إعادة المحاولة' }))
    expect(await screen.findByLabelText('مدة الاحتفاظ الافتراضية')).toHaveValue(365)
  })

  it('saves policy on submit', async () => {
    updatePolicy.mockResolvedValue({ ...samplePolicy, rowVersion: 'rv-2', defaultRetentionDays: 400 })
    renderPage()
    const user = userEvent.setup()
    const input = await screen.findByLabelText('مدة الاحتفاظ الافتراضية')
    await user.clear(input)
    await user.type(input, '400')
    await user.click(screen.getByRole('button', { name: 'حفظ السياسة' }))
    await waitFor(() => expect(updatePolicy).toHaveBeenCalled())
    expect(updatePolicy.mock.calls[0][0].defaultRetentionDays).toBe(400)
    expect(await screen.findByText('تم حفظ سياسة الحوكمة.')).toBeInTheDocument()
  })

  it('shows conflict on 409', async () => {
    updatePolicy.mockRejectedValue(new ApiError(409, 'تعارض'))
    renderPage()
    await userEvent.setup().click(await screen.findByRole('button', { name: 'حفظ السياسة' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('تم تعديل السياسة من مستخدم آخر')
  })
})
