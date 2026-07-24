import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { NoteRoutingSettingsPage } from './NoteRoutingSettingsPage'

const { listRules, createRule, noteTypes } = vi.hoisted(() => ({
  listRules: vi.fn(),
  createRule: vi.fn(),
  noteTypes: vi.fn(async () => [{
    id: '11111111-1111-4111-8111-111111111111',
    code: 'SECURITY',
    nameAr: 'أمنية',
    descriptionAr: null,
    entryInstructionsAr: null,
    sortOrder: 1,
    isActive: true,
    defaultSeverity: 2,
    defaultSeverityAr: 'عالية',
    defaultDueDays: 3,
    rowVersion: 'rv',
  }]),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => [
    'Notes.ViewRouting',
    'Notes.ManageRoutingRules',
    'Notes.ActivateRoutingRules',
  ].includes(code),
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      noteTypes,
      noteRoutingRules: {
        ...actual.api.noteRoutingRules,
        list: listRules,
        create: createRule,
      },
    },
  }
})

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <NoteRoutingSettingsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('NoteRoutingSettingsPage', () => {
  beforeEach(() => {
    listRules.mockReset()
    createRule.mockReset()
    listRules.mockResolvedValue({ items: [], page: 1, pageSize: 50, totalCount: 0 })
    createRule.mockResolvedValue({ id: 'rule-1' })
  })

  it('shows empty state for routing rules', async () => {
    renderPage()
    expect(await screen.findByText('لا توجد قواعد توجيه.')).toBeInTheDocument()
  })

  it('validates required fields before submit', async () => {
    renderPage()
    await userEvent.click(screen.getByRole('button', { name: 'حفظ القاعدة' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('رمز القاعدة مطلوب.')
    expect(createRule).not.toHaveBeenCalled()
  })

  it('creates a department routing rule', async () => {
    renderPage()
    const user = userEvent.setup()

    await user.type(screen.getByLabelText('الرمز'), 'SEC-FAC')
    await user.type(screen.getByLabelText('الاسم'), 'توجيه أمني')
    await user.selectOptions(screen.getByLabelText('نوع الملاحظة'), '11111111-1111-4111-8111-111111111111')
    await user.type(screen.getByLabelText('ProcessingDepartmentId'), '22222222-2222-4222-8222-222222222222')
    await user.type(screen.getByLabelText('السبب'), 'إضافة قاعدة توجيه')
    await user.click(screen.getByRole('button', { name: 'حفظ القاعدة' }))

    await waitFor(() => expect(createRule).toHaveBeenCalled())
    expect(createRule.mock.calls[0][0]).toMatchObject({
      code: 'SEC-FAC',
      nameAr: 'توجيه أمني',
      noteTypeId: '11111111-1111-4111-8111-111111111111',
      processingTargetType: 0,
      processingDepartmentId: '22222222-2222-4222-8222-222222222222',
      reason: 'إضافة قاعدة توجيه',
    })
  })
})
