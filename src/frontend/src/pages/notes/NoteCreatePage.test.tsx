import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Me } from '../../api/client'
import { NoteCreatePage } from './NoteCreatePage'

const NOTE_TYPE_ID = '44444444-4444-4444-4444-444444444403'

const { createNote, listRegions, listFacilities, listDepartments, myNoteTypes, myIntakeContext, myIntakeFacilities, navigateMock } = vi.hoisted(() => ({
  createNote: vi.fn(),
  listRegions: vi.fn(async () => ({
    items: [{ id: '11111111-1111-4111-8111-111111111111', code: 'RG-1', nameAr: 'منطقة الرياض', isActive: true, createdAtUtc: '', rowVersion: '' }],
    page: 1,
    pageSize: 50,
    totalCount: 1,
  })),
  listFacilities: vi.fn(async () => ({
    items: [{ id: '22222222-2222-4222-8222-222222222222', regionId: '11111111-1111-4111-8111-111111111111', code: 'F-1', nameAr: 'سجن الرياض', isActive: true, rowVersion: '' }],
    page: 1,
    pageSize: 50,
    totalCount: 1,
  })),
  listDepartments: vi.fn(async () => ({
    items: [{ id: 'dept-1', organizationId: 'org-1', code: 'D-1', nameAr: 'إدارة الصيانة', isActive: true }],
    page: 1,
    pageSize: 100,
    totalCount: 1,
  })),
  myNoteTypes: vi.fn(async () => [{
    id: NOTE_TYPE_ID,
    code: 'OPERATIONAL',
    nameAr: 'تشغيلية',
    descriptionAr: 'ملاحظات تشغيلية',
    entryInstructionsAr: 'اكتب الأثر التشغيلي.',
    sortOrder: 30,
    isActive: true,
    defaultSeverity: 1,
    defaultSeverityAr: 'متوسطة',
    defaultDueDays: 5,
    rowVersion: 'rv',
  }]),
  myIntakeContext: vi.fn(async () => ({
    lockType: 0,
    lockedRegionId: null,
    lockedRegionNameAr: null,
    lockedFacilityId: null,
    lockedFacilityNameAr: null,
    regions: [{ id: '11111111-1111-4111-8111-111111111111', nameAr: 'منطقة الرياض' }],
    creatableNoteTypes: [{
      id: NOTE_TYPE_ID,
      code: 'OPERATIONAL',
      nameAr: 'تشغيلية',
      descriptionAr: 'ملاحظات تشغيلية',
      entryInstructionsAr: 'اكتب الأثر التشغيلي.',
      sortOrder: 30,
      isActive: true,
      defaultSeverity: 1,
      defaultSeverityAr: 'متوسطة',
      defaultDueDays: 5,
      rowVersion: 'rv',
    }],
  })),
  myIntakeFacilities: vi.fn(async () => [{ id: '22222222-2222-4222-8222-222222222222', regionId: '11111111-1111-4111-8111-111111111111', nameAr: 'سجن الرياض' }]),
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
      departments: listDepartments,
      myNoteTypes,
      myNoteIntakeContext: myIntakeContext,
      myNoteIntakeFacilities: myIntakeFacilities,
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
    listFacilities.mockClear()
    myNoteTypes.mockClear()
    myIntakeContext.mockClear()
    myIntakeFacilities.mockClear()
    navigateMock.mockReset()
  })

  it('shows Arabic validation errors when required fields are missing', async () => {
    renderPage()
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'حفظ المسودة' }))

    expect(await screen.findByText('العنوان مطلوب.')).toBeInTheDocument()
    expect(screen.getByText('الوصف مطلوب.')).toBeInTheDocument()
    expect(screen.getByText('يجب اختيار المنطقة أولًا.')).toBeInTheDocument()
    expect(screen.getByText('يجب اختيار السجن.')).toBeInTheDocument()
    expect(screen.getByText('نوع الملاحظة مطلوب.')).toBeInTheDocument()
    expect(createNote).not.toHaveBeenCalled()
  })

  it('shows region then facility then note type in the create flow', async () => {
    renderPage()
    const user = userEvent.setup()

    expect(await screen.findByLabelText('المنطقة')).toBeInTheDocument()
    await user.selectOptions(await screen.findByRole('option', { name: 'منطقة الرياض' }).then(() => screen.getByLabelText('المنطقة')), '11111111-1111-4111-8111-111111111111')
    expect(await screen.findByLabelText('السجن')).toBeInTheDocument()
    expect(await screen.findByRole('option', { name: 'تشغيلية' })).toBeInTheDocument()
  })

  it('loads facilities only after a region is selected', async () => {
    renderPage()
    const user = userEvent.setup()

    expect(myIntakeFacilities).not.toHaveBeenCalled()
    expect(listFacilities).not.toHaveBeenCalled()

    await user.selectOptions(await screen.findByRole('option', { name: 'منطقة الرياض' }).then(() => screen.getByLabelText('المنطقة')), '11111111-1111-4111-8111-111111111111')
    await waitFor(() => expect(myIntakeFacilities).toHaveBeenCalledWith('11111111-1111-4111-8111-111111111111'))
    expect(listFacilities).not.toHaveBeenCalled()
    expect(await screen.findByRole('option', { name: 'سجن الرياض' })).toBeInTheDocument()
  })

  it('submits with numeric enum values and navigates to the new note on success', async () => {
    createNote.mockResolvedValue({ id: 'note-42' })
    renderPage()
    const user = userEvent.setup()

    await user.type(screen.getByLabelText('العنوان'), 'ملاحظة تجريبية')
    await user.type(screen.getByLabelText('الوصف'), 'وصف تفصيلي كافٍ.')
    await user.selectOptions(await screen.findByRole('option', { name: 'منطقة الرياض' }).then(() => screen.getByLabelText('المنطقة')), '11111111-1111-4111-8111-111111111111')
    await user.selectOptions(await screen.findByRole('option', { name: 'سجن الرياض' }).then(() => screen.getByLabelText('السجن')), '22222222-2222-4222-8222-222222222222')
    await user.selectOptions(screen.getByLabelText('نوع الملاحظة'), NOTE_TYPE_ID)
    await user.click(screen.getByRole('button', { name: 'حفظ المسودة' }))

    await waitFor(() => expect(createNote).toHaveBeenCalledTimes(1))
    const payload = createNote.mock.calls[0][0]
    expect(payload).toMatchObject({
      title: 'ملاحظة تجريبية',
      description: 'وصف تفصيلي كافٍ.',
      noteTypeId: NOTE_TYPE_ID,
      severity: 1,
      sourceType: 0,
      classification: 0,
      scopeType: 3,
      regionId: '11111111-1111-4111-8111-111111111111',
      facilityId: '22222222-2222-4222-8222-222222222222',
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
    await user.selectOptions(await screen.findByRole('option', { name: 'منطقة الرياض' }).then(() => screen.getByLabelText('المنطقة')), '11111111-1111-4111-8111-111111111111')
    await user.selectOptions(await screen.findByRole('option', { name: 'سجن الرياض' }).then(() => screen.getByLabelText('السجن')), '22222222-2222-4222-8222-222222222222')
    await user.selectOptions(screen.getByLabelText('نوع الملاحظة'), NOTE_TYPE_ID)

    const submitButton = screen.getByRole('button', { name: 'حفظ المسودة' })
    await user.click(submitButton)
    await user.click(submitButton)
    await user.click(submitButton)

    await waitFor(() => expect(createNote).toHaveBeenCalledTimes(1))
    resolveCreate({ id: 'note-1' })
  })
})
