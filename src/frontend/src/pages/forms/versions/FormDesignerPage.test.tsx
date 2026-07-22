import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FormDesignerPage } from './FormDesignerPage'

const { getVersion, currentPermissions } = vi.hoisted(() => ({
  getVersion: vi.fn(),
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
        autosaveSchema: vi.fn(),
        validateVersion: vi.fn(),
        submitVersionReview: vi.fn(),
      },
    },
  }
})

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/forms/f1/versions/v1/edit']}>
        <Routes>
          <Route path="/forms/:formId/versions/:versionId/edit" element={<FormDesignerPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormDesignerPage', () => {
  beforeEach(() => {
    currentPermissions.clear()
    currentPermissions.add('Forms.UpdateDraft')
    getVersion.mockReset()
  })

  it('blocks without permission', () => {
    currentPermissions.clear()
    renderPage()
    expect(screen.getByRole('alert')).toHaveTextContent('ليست لديك صلاحية تصميم النماذج.')
  })

  it('loads designer canvas', async () => {
    getVersion.mockResolvedValue({
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
      allowedActions: ['UpdateDraft'],
    })
    renderPage()
    await waitFor(() => expect(screen.getByText('مصمم النموذج — v1')).toBeInTheDocument())
    expect(screen.getByText('حقل نصي')).toBeInTheDocument()
  })
})
