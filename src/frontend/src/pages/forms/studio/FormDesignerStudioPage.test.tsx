import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../api/client'
import { createEmptySchema } from '../../../forms/designer/schemaTypes'
import { FormDesignerStudioPage } from './FormDesignerStudioPage'

const {
  createForm,
  createVersion,
  getForm,
  getVersion,
  listVersions,
  autosaveSchema,
  validateVersion,
  listRegions,
} = vi.hoisted(() => ({
  createForm: vi.fn(),
  createVersion: vi.fn(),
  getForm: vi.fn(),
  getVersion: vi.fn(),
  listVersions: vi.fn(),
  autosaveSchema: vi.fn(),
  validateVersion: vi.fn(async () => ({ isValid: true, schemaHash: 'h', issues: [], pageCount: 1, sectionCount: 1, fieldCount: 1, calculatedFieldCount: 0, conditionCount: 0 })),
  listRegions: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, totalCount: 0 })),
}))

vi.mock('../../../auth/AuthProvider', () => ({
  usePermission: (code: string) => ['Forms.UpdateDraft', 'Forms.Create', 'Forms.ViewVersionHistory'].includes(code),
}))

const layoutModeMock = vi.hoisted(() => ({ current: 'desktop' as 'desktop' | 'tablet' | 'mobile' }))
vi.mock('../../../forms/designer/useResponsiveStudioLayout', () => ({
  useResponsiveStudioLayout: () => layoutModeMock.current,
}))

vi.mock('../../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../../api/client')>('../../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      regions: listRegions,
      forms: {
        ...actual.api.forms,
        create: createForm,
        createVersion,
        get: getForm,
        getVersion,
        listVersions,
        autosaveSchema,
        validateVersion,
      },
    },
  }
})

function renderStudio(initialPath: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route path="/forms/designer/new" element={<FormDesignerStudioPage />} />
          <Route path="/forms/designer/:formId" element={<FormDesignerStudioPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const sampleVersion = {
  id: 'v1',
  formDefinitionId: 'form-1',
  versionNumber: 1,
  status: 0,
  statusAr: 'مسودة',
  basedOnVersionId: null,
  draftSchemaJson: JSON.stringify(createEmptySchema()),
  draftSchemaHash: 'hash',
  schemaFormatVersion: 1,
  createdByUserId: 'u1',
  updatedByUserId: null,
  createdAtUtc: '2026-01-01T00:00:00Z',
  lastSavedAtUtc: null,
  submittedForReviewAtUtc: null,
  approvedAtUtc: null,
  approvedByUserId: null,
  snapshotId: null,
  rowVersion: 'rv-1',
  allowedActions: ['UpdateDraft', 'SaveSchema', 'Autosave', 'Validate', 'SubmitForReview'],
}

const sampleForm = {
  id: 'form-1',
  code: 'FORM-1',
  nameAr: 'نموذج تجريبي',
  nameEn: null,
  description: 'وصف',
  status: 0,
  statusAr: 'مسودة',
  classification: 0,
  scopeType: 0,
  createdByUserId: 'u1',
  createdAtUtc: '2026-01-01T00:00:00Z',
  rowVersion: 'rv-form',
  isSensitiveRedacted: false,
  allowedActions: ['UpdateDraft'],
}

beforeEach(() => {
  autosaveSchema.mockResolvedValue({ ...sampleVersion, rowVersion: 'rv-2' })
})

afterEach(() => {
  vi.clearAllMocks()
  layoutModeMock.current = 'desktop'
})

describe('FormDesignerStudioPage — new form start flow', () => {
  it('creates a blank form and lands in the studio for its first version', async () => {
    const user = userEvent.setup()
    createForm.mockResolvedValue(sampleForm)
    createVersion.mockResolvedValue(sampleVersion)
    getForm.mockResolvedValue(sampleForm)
    getVersion.mockResolvedValue(sampleVersion)

    renderStudio('/forms/designer/new')

    await user.click(screen.getByRole('button', { name: /^نموذج فارغ/ }))
    await user.type(screen.getByLabelText('اسم النموذج *'), 'نموذج جديد')
    await user.type(screen.getByLabelText('الغرض أو الوصف *'), 'الغرض من النموذج')
    await user.selectOptions(screen.getByLabelText('نطاق النموذج *'), '0')

    await user.click(screen.getByRole('button', { name: 'إنشاء وفتح الاستوديو' }))

    await waitFor(() => expect(createForm).toHaveBeenCalled())
    await waitFor(() => expect(createVersion).toHaveBeenCalledWith('form-1'))
    await waitFor(() => expect(screen.getByText('نموذج تجريبي')).toBeInTheDocument())
  })
})

describe('FormDesignerStudioPage — existing form', () => {
  beforeEach(() => {
    getForm.mockResolvedValue(sampleForm)
    getVersion.mockResolvedValue(sampleVersion)
    listVersions.mockResolvedValue([{ ...sampleVersion }])
  })

  it('shows a "توجد تغييرات غير محفوظة" status and then "تم الحفظ" after autosave resolves', async () => {
    const user = userEvent.setup()
    renderStudio('/forms/designer/form-1?versionId=v1')

    await user.click(await screen.findByRole('button', { name: 'حقل نصي' }))
    const labelInput = await screen.findByLabelText('عنوان الحقل')
    await user.clear(labelInput)
    await user.type(labelInput, 'تسمية جديدة')
    await user.tab()

    await waitFor(() => expect(screen.getByText(/توجد تغييرات غير محفوظة/)).toBeInTheDocument())
    await waitFor(() => expect(autosaveSchema).toHaveBeenCalled(), { timeout: 3000 })
    await waitFor(() => expect(screen.getByText(/تم الحفظ/)).toBeInTheDocument())
  })

  it('shows the three non-destructive conflict actions on a 409 autosave response', async () => {
    const user = userEvent.setup()
    autosaveSchema.mockRejectedValue(new ApiError(409, 'تعارض'))
    getVersion.mockResolvedValue(sampleVersion)
    renderStudio('/forms/designer/form-1?versionId=v1')

    await user.click(await screen.findByRole('button', { name: 'حقل نصي' }))
    const labelInput = await screen.findByLabelText('عنوان الحقل')
    await user.clear(labelInput)
    await user.type(labelInput, 'تسمية أخرى')
    await user.tab()

    await waitFor(() => expect(screen.getByText('تم تعديل المسودة من مستخدم آخر منذ آخر تحميل لهذه الصفحة.')).toBeInTheDocument(), { timeout: 3000 })
    expect(screen.getByRole('button', { name: 'تحميل النسخة الأحدث' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'مقارنة التغييرات' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /حفظ نسخة جديدة/ })).toBeInTheDocument()
  })

  it('renders the mobile review-only mode with an explicit message that advanced structuring needs a bigger screen, and no drag-and-drop affordance', async () => {
    layoutModeMock.current = 'mobile'
    renderStudio('/forms/designer/form-1?versionId=v1')

    expect(await screen.findByText(/الهيكلة المتقدمة .* تتطلب شاشة أكبر/)).toBeInTheDocument()
    expect(screen.queryByLabelText('سحب لإعادة الترتيب')).not.toBeInTheDocument()
  })
})
