import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, type FormDetail } from '../../api/client'
import { FormEditPage } from './FormEditPage'

const { getForm, updateForm, listDepartments, navigateMock } = vi.hoisted(() => ({
  getForm: vi.fn(),
  updateForm: vi.fn(),
  listDepartments: vi.fn(async () => ({ items: [], page: 1, pageSize: 100, totalCount: 0 })),
  navigateMock: vi.fn(),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Forms.UpdateDraft',
}))

vi.mock('react-router', async () => {
  const actual = await vi.importActual<typeof import('react-router')>('react-router')
  return { ...actual, useNavigate: () => navigateMock }
})

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      departments: listDepartments,
      forms: { ...actual.api.forms, get: getForm, update: updateForm },
    },
  }
})

const baseForm: FormDetail = {
  id: 'form-1',
  code: 'INCIDENT.REPORT',
  nameAr: 'نموذج واقعة',
  nameEn: null,
  description: 'وصف.',
  status: 0,
  statusAr: 'مسودة',
  classification: 0,
  scopeType: 3,
  regionId: 'region-1',
  facilityId: 'fac-1',
  facilityUnitId: null,
  ownerDepartmentId: null,
  createdByUserId: 'user-1',
  createdByDisplayName: 'مصمم',
  createdAtUtc: '2024-01-01T00:00:00Z',
  rowVersion: 'rv-1',
  isSensitiveRedacted: false,
  allowedActions: ['UpdateDraft'],
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/forms/form-1/edit']}>
        <Routes>
          <Route path="/forms/:id/edit" element={<FormEditPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormEditPage', () => {
  beforeEach(() => {
    getForm.mockReset()
    updateForm.mockReset()
    navigateMock.mockReset()
  })

  it('shows forbidden when form is not editable', async () => {
    getForm.mockResolvedValue({ ...baseForm, status: 3, statusAr: 'معتمد' })
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent('لا يمكن تعديل نموذج في هذه الحالة')
  })

  it('shows conflict message on 409', async () => {
    getForm.mockResolvedValue(baseForm)
    updateForm.mockRejectedValue(new ApiError(409, 'تعارض'))
    renderPage()
    await waitFor(() => expect(screen.getByLabelText('الاسم (عربي)')).toHaveValue('نموذج واقعة'))
    await userEvent.setup().click(screen.getByRole('button', { name: 'حفظ التعديلات' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('تم تعديل النموذج من مستخدم آخر')
  })

  it('navigates to detail on successful update', async () => {
    getForm.mockResolvedValue(baseForm)
    updateForm.mockResolvedValue({ ...baseForm, nameAr: 'محدّث' })
    renderPage()
    await waitFor(() => expect(screen.getByLabelText('الاسم (عربي)')).toHaveValue('نموذج واقعة'))
    await userEvent.setup().click(screen.getByRole('button', { name: 'حفظ التعديلات' }))
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/forms/form-1'))
  })
})
