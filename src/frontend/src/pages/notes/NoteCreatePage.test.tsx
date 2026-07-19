import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Me } from '../../api/client'
import { NoteCreatePage } from './NoteCreatePage'

const { createNote, listRegions, listFacilities, listFacilityUnits, listDepartments, navigateMock } = vi.hoisted(() => ({
  createNote: vi.fn(),
  listRegions: vi.fn(async () => ({
    items: [{ id: 'region-1', code: 'RG-1', nameAr: 'منطقة الرياض', isActive: true, createdAtUtc: '', rowVersion: '' }],
    page: 1,
    pageSize: 50,
    totalCount: 1,
  })),
  listFacilities: vi.fn(async () => ({
    items: [{ id: 'facility-1', regionId: 'region-1', code: 'F-1', nameAr: 'سجن الرياض', isActive: true, rowVersion: '' }],
    page: 1,
    pageSize: 50,
    totalCount: 1,
  })),
  listFacilityUnits: vi.fn(async () => ({
    items: [{ id: 'unit-1', facilityId: 'facility-1', code: 'U-1', nameAr: 'الوحدة الأولى', isActive: true }],
    page: 1,
    pageSize: 100,
    totalCount: 1,
  })),
  listDepartments: vi.fn(async () => ({
    items: [{ id: 'dept-1', organizationId: 'org-1', code: 'D-1', nameAr: 'إدارة الصيانة', isActive: true }],
    page: 1,
    pageSize: 100,
    totalCount: 1,
  })),
  navigateMock: vi.fn(),
}))

const nationalMe: Me = {
  id: 'user-1',
  displayNameAr: 'مستخدم وطني',
  permissions: ['Notes.Create'],
  scopes: [{ id: 's1', scopeType: 0, isActive: true }],
}

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Notes.Create',
  useAuth: () => ({ me: nationalMe }),
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
      facilityUnits: listFacilityUnits,
      departments: listDepartments,
      notes: { ...actual.api.notes, create: createNote },
    },
  }
})

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <NoteCreatePage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('NoteCreatePage', () => {
  beforeEach(() => {
    createNote.mockReset()
    navigateMock.mockReset()
  })

  it('shows Arabic validation errors when required fields are missing', async () => {
    renderPage()
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'حفظ المسودة' }))

    expect(await screen.findByText('العنوان مطلوب.')).toBeInTheDocument()
    expect(screen.getByText('الوصف مطلوب.')).toBeInTheDocument()
    expect(screen.getByText('نطاق الملاحظة مطلوب.')).toBeInTheDocument()
    expect(createNote).not.toHaveBeenCalled()
  })

  it('reveals region and facility fields only for scope types that need them', async () => {
    renderPage()
    const user = userEvent.setup()

    expect(screen.queryByLabelText('المنطقة')).not.toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('نطاق الملاحظة'), '2')
    expect(await screen.findByLabelText('المنطقة')).toBeInTheDocument()
    expect(screen.queryByLabelText('السجن')).not.toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('نطاق الملاحظة'), '3')
    expect(await screen.findByLabelText('السجن')).toBeInTheDocument()
    expect(screen.queryByLabelText('الوحدة')).not.toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('نطاق الملاحظة'), '4')
    expect(await screen.findByLabelText('الوحدة')).toBeInTheDocument()
  })

  it('loads facility units only after a facility is selected, for the FacilityUnit scope', async () => {
    renderPage()
    const user = userEvent.setup()

    await user.selectOptions(screen.getByLabelText('نطاق الملاحظة'), '4')
    const unitSelect = await screen.findByLabelText('الوحدة')
    expect(unitSelect).toBeDisabled()
    expect(listFacilityUnits).not.toHaveBeenCalled()

    await user.selectOptions(await screen.findByLabelText('السجن'), 'facility-1')
    await waitFor(() => expect(listFacilityUnits).toHaveBeenCalledWith('facility-1'))
    expect(await screen.findByRole('option', { name: 'الوحدة الأولى' })).toBeInTheDocument()
  })

  it('submits with numeric enum values and navigates to the new note on success', async () => {
    createNote.mockResolvedValue({ id: 'note-42' })
    renderPage()
    const user = userEvent.setup()

    await user.type(screen.getByLabelText('العنوان'), 'ملاحظة تجريبية')
    await user.type(screen.getByLabelText('الوصف'), 'وصف تفصيلي كافٍ.')
    await user.selectOptions(screen.getByLabelText('نطاق الملاحظة'), '0')
    await user.click(screen.getByRole('button', { name: 'حفظ المسودة' }))

    await waitFor(() => expect(createNote).toHaveBeenCalledTimes(1))
    const payload = createNote.mock.calls[0][0]
    expect(payload).toMatchObject({
      title: 'ملاحظة تجريبية',
      description: 'وصف تفصيلي كافٍ.',
      category: 0,
      severity: 0,
      sourceType: 0,
      classification: 0,
      scopeType: 0,
    })
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/notes/note-42'))
  })

  it('prevents double submission while the create request is pending', async () => {
    let resolveCreate!: (value: unknown) => void
    createNote.mockImplementation(() => new Promise((resolve) => { resolveCreate = resolve }))
    renderPage()
    const user = userEvent.setup()

    await user.type(screen.getByLabelText('العنوان'), 'ملاحظة تجريبية')
    await user.type(screen.getByLabelText('الوصف'), 'وصف تفصيلي كافٍ.')
    await user.selectOptions(screen.getByLabelText('نطاق الملاحظة'), '0')

    const submitButton = screen.getByRole('button', { name: 'حفظ المسودة' })
    await user.click(submitButton)
    await user.click(submitButton)
    await user.click(submitButton)

    await waitFor(() => expect(createNote).toHaveBeenCalledTimes(1))
    resolveCreate({ id: 'note-1' })
  })
})
