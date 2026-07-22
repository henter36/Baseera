import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, type FormDetail } from '../../api/client'

const { getForm, retentionStatus, submitReview, currentPermissions } = vi.hoisted(() => ({
  getForm: vi.fn(),
  retentionStatus: vi.fn(),
  submitReview: vi.fn(),
  currentPermissions: new Set<string>(['Forms.View']),
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
      regions: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
      facilities: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
      forms: {
        ...actual.api.forms,
        get: getForm,
        retentionStatus,
        submitReview,
      },
    },
  }
})

import { FormDetailPage } from './FormDetailPage'

const baseForm: FormDetail = {
  id: 'form-1',
  code: 'INCIDENT.REPORT',
  nameAr: 'نموذج واقعة',
  nameEn: null,
  description: 'وصف تفصيلي.',
  status: 0,
  statusAr: 'مسودة',
  classification: 0,
  scopeType: 0,
  createdByUserId: 'user-1',
  createdByDisplayName: 'مصمم',
  createdAtUtc: '2024-01-01T00:00:00Z',
  rowVersion: 'rv-1',
  isSensitiveRedacted: false,
  allowedActions: ['UpdateDraft', 'SubmitForReview'],
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/forms/form-1']}>
        <Routes>
          <Route path="/forms/:id" element={<FormDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormDetailPage', () => {
  beforeEach(() => {
    getForm.mockReset()
    retentionStatus.mockReset()
    submitReview.mockReset()
    currentPermissions.clear()
    currentPermissions.add('Forms.View')
    retentionStatus.mockResolvedValue({
      formDefinitionId: 'form-1',
      isRetentionApplicable: true,
      retentionDays: 365,
      isExpired: false,
      isEligibleForArchive: false,
    })
  })

  it('shows loading then form details', async () => {
    getForm.mockResolvedValue(baseForm)
    renderPage()
    expect(await screen.findByText('نموذج واقعة')).toBeInTheDocument()
    expect(screen.getByText('INCIDENT.REPORT')).toBeInTheDocument()
    expect(screen.getByText('وصف تفصيلي.')).toBeInTheDocument()
  })

  it('shows 404 message when form is missing', async () => {
    getForm.mockRejectedValue(new ApiError(404, 'غير موجود'))
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent('النموذج غير موجود أو خارج نطاقك.')
  })

  it('shows edit link when UpdateDraft is allowed', async () => {
    getForm.mockResolvedValue(baseForm)
    renderPage()
    expect(await screen.findByRole('link', { name: 'تعديل' })).toBeInTheDocument()
  })

  it('requires reason before submit for review', async () => {
    getForm.mockResolvedValue(baseForm)
    renderPage()
    await screen.findByText('إرسال للمراجعة')
    await userEvent.setup().click(screen.getByRole('button', { name: 'إرسال للمراجعة' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('السبب مطلوب.')
    expect(submitReview).not.toHaveBeenCalled()
  })

  it('shows manage access link when permitted', async () => {
    currentPermissions.add('Forms.ManageAccess')
    getForm.mockResolvedValue(baseForm)
    renderPage()
    expect(await screen.findByRole('link', { name: 'إدارة الوصول' })).toBeInTheDocument()
  })
})
