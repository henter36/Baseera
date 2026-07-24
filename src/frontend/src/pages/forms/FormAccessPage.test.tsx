import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, type FormDetail } from '../../api/client'
import { FormAccessPage } from './FormAccessPage'

const { getForm, accessGrants, createAccessGrant, listRegions, listFacilities, listDepartments } = vi.hoisted(() => ({
  getForm: vi.fn(),
  accessGrants: vi.fn(),
  createAccessGrant: vi.fn(),
  listRegions: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  listFacilities: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
  listDepartments: vi.fn(async () => ({ items: [], page: 1, pageSize: 100, totalCount: 0 })),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Forms.ManageAccess',
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      regions: listRegions,
      facilities: listFacilities,
      departments: listDepartments,
      forms: {
        ...actual.api.forms,
        get: getForm,
        accessGrants,
        createAccessGrant,
      },
    },
  }
})

const baseForm: FormDetail = {
  id: 'form-1',
  code: 'INCIDENT.REPORT',
  nameAr: 'نموذج واقعة',
  nameEn: null,
  description: 'وصف.',
  status: 3,
  statusAr: 'معتمد',
  classification: 0,
  scopeType: 0,
  createdByUserId: 'user-1',
  createdAtUtc: '2024-01-01T00:00:00Z',
  rowVersion: 'rv-1',
  isSensitiveRedacted: false,
  allowedActions: [],
}

const PRINCIPAL_ID = '33333333-3333-4333-8333-333333333333'

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/forms/form-1/access']}>
        <Routes>
          <Route path="/forms/:id/access" element={<FormAccessPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormAccessPage', () => {
  beforeEach(() => {
    getForm.mockReset()
    accessGrants.mockReset()
    createAccessGrant.mockReset()
    getForm.mockResolvedValue(baseForm)
    accessGrants.mockResolvedValue([])
  })

  it('shows empty grants state', async () => {
    renderPage()
    expect(await screen.findByText('لا توجد منح وصول.')).toBeInTheDocument()
  })

  it('validates grant form fields', async () => {
    renderPage()
    expect(await screen.findByText('لا توجد منح وصول.')).toBeInTheDocument()
    await userEvent.setup().click(screen.getByRole('button', { name: 'إضافة منح' }))
    expect(await screen.findByText('المستفيد مطلوب.')).toBeInTheDocument()
    expect(createAccessGrant).not.toHaveBeenCalled()
  })

  it('creates a grant on valid submit', async () => {
    createAccessGrant.mockResolvedValue({ id: 'grant-1' })
    renderPage()
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('معرف المستفيد'), PRINCIPAL_ID)
    await user.type(screen.getByLabelText('سبب المنح'), 'منح مراجعة')
    await user.click(screen.getByRole('button', { name: 'إضافة منح' }))
    await waitFor(() => expect(createAccessGrant).toHaveBeenCalled())
    expect(createAccessGrant.mock.calls[0][1]).toMatchObject({
      principalId: PRINCIPAL_ID,
      reason: 'منح مراجعة',
    })
  })

  it('shows error when form is not found', async () => {
    getForm.mockRejectedValue(new ApiError(404, 'غير موجود'))
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent('النموذج غير موجود أو خارج نطاقك.')
  })
})
