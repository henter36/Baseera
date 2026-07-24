import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FormDesignerPage } from './FormDesignerPage'

const { getVersion, autosaveSchema, submitVersionReview, currentPermissions } = vi.hoisted(() => ({
  getVersion: vi.fn(),
  autosaveSchema: vi.fn(),
  submitVersionReview: vi.fn(),
  currentPermissions: new Set<string>(['Forms.UpdateDraft']),
}))

vi.mock('../../../auth/AuthProvider', () => ({
  usePermission: (code: string) => currentPermissions.has(code),
}))

vi.mock('../../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../../api/client')>('../../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      forms: {
        ...actual.api.forms,
        getVersion,
        autosaveSchema,
        validateVersion: vi.fn(),
        submitVersionReview,
      },
    },
  }
})

const baseVersion = {
  id: 'v1',
  formDefinitionId: 'f1',
  versionNumber: 1,
  status: 0,
  statusAr: 'مسودة',
  schemaFormatVersion: 1,
  draftSchemaJson: JSON.stringify({
    schemaFormatVersion: 1,
    pages: [{
      id: 'p1', key: 'page1', titleAr: 'الصفحة 1', order: 0,
      sections: [{
        id: 's1', key: 'section1', titleAr: 'القسم 1', order: 0,
        fields: [{
          id: 'fld1', key: 'field1', type: 0, labelAr: 'حقل نصي', order: 0,
          layoutWidth: 0, isRequired: false, validationRules: [], isReadOnly: false, isCalculated: false,
        }],
      }],
    }],
  }),
  createdAtUtc: new Date().toISOString(),
  rowVersion: 'AAAA',
  allowedActions: ['SaveSchema', 'SubmitForReview'],
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const view = render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/forms/f1/versions/v1/edit']}>
        <Routes>
          <Route path="/forms/:formId/versions/:versionId/edit" element={<FormDesignerPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { client, ...view }
}

describe('FormDesignerPage', () => {
  beforeEach(() => {
    currentPermissions.clear()
    currentPermissions.add('Forms.UpdateDraft')
    getVersion.mockReset()
    autosaveSchema.mockReset()
    submitVersionReview.mockReset()
    autosaveSchema.mockResolvedValue({ rowVersion: 'BBBB' })
    submitVersionReview.mockResolvedValue({})
  })

  it('blocks without permission', () => {
    currentPermissions.clear()
    renderPage()
    expect(screen.getByRole('alert')).toHaveTextContent('ليست لديك صلاحية تصميم النماذج.')
  })

  it('loads designer canvas', async () => {
    getVersion.mockResolvedValue(baseVersion)
    renderPage()
    expect(await screen.findByText('مصمم النموذج — v1')).toBeInTheDocument()
    expect(screen.getByText('حقل نصي')).toBeInTheDocument()
  })

  it('flushes autosave before submit with updated row version', async () => {
    getVersion.mockResolvedValue(baseVersion)
    renderPage()
    await screen.findByText('مصمم النموذج — v1')

    await userEvent.click(screen.getByRole('button', { name: 'حقل نصي (نص قصير)' }))
    const input = await screen.findByLabelText('التسمية العربية')
    await userEvent.clear(input)
    await userEvent.type(input, 'حقل معدّل')
    fireEvent.blur(input)

    fireEvent.click(screen.getByRole('button', { name: 'إرسال للمراجعة' }))

    await waitFor(() => {
      expect(autosaveSchema).toHaveBeenCalledWith('f1', 'v1', expect.objectContaining({ rowVersion: 'AAAA' }))
    })
    await waitFor(() => {
      expect(submitVersionReview).toHaveBeenCalledWith('f1', 'v1', expect.objectContaining({ rowVersion: 'BBBB' }))
    })
    expect(autosaveSchema.mock.invocationCallOrder[0]).toBeLessThan(submitVersionReview.mock.invocationCallOrder[0])
  })

  it('preserves undo history when autosave refetches the same version', async () => {
    getVersion.mockResolvedValue(baseVersion)
    const { client } = renderPage()
    await screen.findByText('مصمم النموذج — v1')

    await userEvent.click(screen.getByRole('button', { name: 'حقل نصي (نص قصير)' }))
    const input = await screen.findByLabelText('التسمية العربية')
    await userEvent.clear(input)
    await userEvent.type(input, 'حقل معدّل')
    fireEvent.blur(input)

    expect(screen.getByDisplayValue('حقل معدّل')).toBeInTheDocument()

    getVersion.mockResolvedValue({ ...baseVersion, rowVersion: 'CCCC' })
    await client.invalidateQueries({ queryKey: ['form-version', 'f1', 'v1'] })
    await waitFor(() => expect(getVersion).toHaveBeenCalledTimes(2))

    expect(screen.getByDisplayValue('حقل معدّل')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'تراجع' }))
    expect(screen.getByDisplayValue('حقل نصي')).toBeInTheDocument()
  })
})
