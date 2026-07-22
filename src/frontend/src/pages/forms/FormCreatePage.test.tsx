import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FormCreatePage } from './FormCreatePage'

const REGION_ID = '11111111-1111-4111-8111-111111111111'
const FACILITY_ID = '22222222-2222-4222-8222-222222222222'

const { createForm, listRegions, listFacilities, listDepartments, navigateMock } = vi.hoisted(() => ({
  createForm: vi.fn(),
  listRegions: vi.fn(async () => ({
    items: [{ id: REGION_ID, code: 'RG-1', nameAr: 'منطقة الرياض', isActive: true, createdAtUtc: '', rowVersion: '' }],
    page: 1, pageSize: 50, totalCount: 1,
  })),
  listFacilities: vi.fn(async () => ({
    items: [{ id: FACILITY_ID, regionId: REGION_ID, code: 'F-1', nameAr: 'سجن الرياض', isActive: true, rowVersion: '' }],
    page: 1, pageSize: 50, totalCount: 1,
  })),
  listDepartments: vi.fn(async () => ({ items: [], page: 1, pageSize: 100, totalCount: 0 })),
  navigateMock: vi.fn(),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Forms.Create',
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')
  return { ...actual, useNavigate: () => navigateMock }
})

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      regions: listRegions,
      facilities: listFacilities,
      departments: listDepartments,
      forms: { ...actual.api.forms, create: createForm },
    },
  }
})

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <FormCreatePage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormCreatePage', () => {
  beforeEach(() => {
    createForm.mockReset()
    navigateMock.mockReset()
  })

  it('shows Arabic validation errors when required fields are missing', async () => {
    renderPage()
    await userEvent.setup().click(screen.getByRole('button', { name: 'حفظ المسودة' }))
    expect(await screen.findByText('رمز النموذج مطلوب.')).toBeInTheDocument()
    expect(screen.getByText('اسم النموذج مطلوب.')).toBeInTheDocument()
    expect(screen.getByText('الوصف مطلوب.')).toBeInTheDocument()
    expect(createForm).not.toHaveBeenCalled()
  })

  it('submits with numeric enum values and navigates on success', async () => {
    createForm.mockResolvedValue({ id: 'form-42' })
    renderPage()
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('رمز النموذج'), 'INCIDENT.REPORT')
    await user.type(screen.getByLabelText('الاسم (عربي)'), 'نموذج واقعة')
    await user.type(screen.getByLabelText('الوصف'), 'وصف النموذج.')
    await user.selectOptions(screen.getByLabelText('المنطقة'), REGION_ID)
    await user.selectOptions(screen.getByLabelText('السجن'), FACILITY_ID)
    await user.click(screen.getByRole('button', { name: 'حفظ المسودة' }))
    await waitFor(() => expect(createForm).toHaveBeenCalledTimes(1))
    expect(createForm.mock.calls[0][0]).toMatchObject({
      code: 'INCIDENT.REPORT',
      nameAr: 'نموذج واقعة',
      classification: 0,
      scopeType: 3,
      regionId: REGION_ID,
      facilityId: FACILITY_ID,
    })
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/forms/form-42'))
  })
})
