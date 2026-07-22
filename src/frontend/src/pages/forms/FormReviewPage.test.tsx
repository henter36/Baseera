import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, type FormDetail } from '../../api/client'
import { FormReviewPage } from './FormReviewPage'

const { getForm, reviewDecisions, approveForm, currentPermissions } = vi.hoisted(() => ({
  getForm: vi.fn(),
  reviewDecisions: vi.fn(),
  approveForm: vi.fn(),
  currentPermissions: new Set<string>(['Forms.Review', 'Forms.Approve']),
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
      forms: {
        ...actual.api.forms,
        get: getForm,
        reviewDecisions,
        approve: approveForm,
      },
    },
  }
})

const inReviewForm: FormDetail = {
  id: 'form-1',
  code: 'INCIDENT.REPORT',
  nameAr: 'نموذج واقعة',
  nameEn: null,
  description: 'وصف.',
  status: 1,
  statusAr: 'قيد المراجعة',
  classification: 0,
  scopeType: 0,
  createdByUserId: 'user-1',
  createdAtUtc: '2024-01-01T00:00:00Z',
  rowVersion: 'rv-1',
  isSensitiveRedacted: false,
  allowedActions: ['Approve', 'RequestChanges', 'Reject'],
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/forms/form-1/review']}>
        <Routes>
          <Route path="/forms/:id/review" element={<FormReviewPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormReviewPage', () => {
  beforeEach(() => {
    getForm.mockReset()
    reviewDecisions.mockReset()
    approveForm.mockReset()
    reviewDecisions.mockResolvedValue([])
    getForm.mockResolvedValue(inReviewForm)
    currentPermissions.clear()
    currentPermissions.add('Forms.Review')
    currentPermissions.add('Forms.Approve')
  })

  it('shows forbidden without review permissions', async () => {
    currentPermissions.clear()
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent('ليست لديك صلاحية مراجعة النماذج.')
  })

  it('shows review actions when form is in review', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: 'اعتماد' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'طلب تعديلات' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'رفض' })).toBeInTheDocument()
  })

  it('calls approve API with reason', async () => {
    approveForm.mockResolvedValue({ ...inReviewForm, status: 3 })
    renderPage()
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('سبب القرار'), 'جاهز للاعتماد')
    await user.click(screen.getByRole('button', { name: 'اعتماد' }))
    await waitFor(() => expect(approveForm).toHaveBeenCalledWith('form-1', { reason: 'جاهز للاعتماد', rowVersion: 'rv-1' }))
  })

  it('shows empty review history', async () => {
    renderPage()
    expect(await screen.findByText('لا توجد قرارات مراجعة مسجّلة.')).toBeInTheDocument()
  })

  it('shows message when form is not in review', async () => {
    getForm.mockResolvedValue({ ...inReviewForm, status: 0, statusAr: 'مسودة', allowedActions: [] })
    renderPage()
    expect(await screen.findByText('النموذج ليس في حالة «قيد المراجعة» حاليًا.')).toBeInTheDocument()
  })

  it('shows conflict on 409', async () => {
    approveForm.mockRejectedValue(new ApiError(409, 'تعارض'))
    renderPage()
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('سبب القرار'), 'سبب')
    await user.click(screen.getByRole('button', { name: 'اعتماد' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('تم تغيير النموذج بواسطة مستخدم آخر')
  })
})
