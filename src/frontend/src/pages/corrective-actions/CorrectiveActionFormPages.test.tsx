import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { CorrectiveActionCreatePage } from './CorrectiveActionCreatePage'
import { CorrectiveActionEditPage } from './CorrectiveActionEditPage'

const {
  getNote,
  getAction,
  createAction,
  updateAction,
  listDepartments,
  currentPermissions,
} = vi.hoisted(() => ({
  getNote: vi.fn(),
  getAction: vi.fn(),
  createAction: vi.fn(),
  updateAction: vi.fn(),
  listDepartments: vi.fn(async () => ({ items: [], page: 1, pageSize: 100, totalCount: 0 })),
  currentPermissions: new Set<string>(),
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
      departments: listDepartments,
      notes: { ...actual.api.notes, get: getNote, createCorrectiveAction: createAction },
      correctiveActions: { ...actual.api.correctiveActions, get: getAction, update: updateAction },
    },
  }
})

const note = { id: 'note-1', title: 'ملاحظة أصلية' }
const action = {
  id: 'ca-1',
  referenceNumber: 'CA-00000001',
  operationalNoteId: 'note-1',
  operationalNoteReferenceNumber: 'OBS-00000001',
  title: 'عنوان قائم',
  description: 'وصف قائم',
  priority: 1,
  classification: 0,
  ownerDepartmentId: null,
  dueAtUtc: null,
  rowVersion: 'row-v1',
}

function renderWithRoute(element: React.ReactElement, path: string, route: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path={route} element={element} />
          <Route path="/corrective-actions/:id" element={<div>تم الحفظ</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('Corrective action form pages', () => {
  beforeEach(() => {
    getNote.mockReset()
    getAction.mockReset()
    createAction.mockReset()
    updateAction.mockReset()
    listDepartments.mockClear()
    currentPermissions.clear()
  })

  it('validates create fields and submits a valid request', async () => {
    currentPermissions.add('CorrectiveActions.Create')
    getNote.mockResolvedValue(note)
    createAction.mockResolvedValue({ id: 'ca-1' })
    renderWithRoute(<CorrectiveActionCreatePage />, '/notes/note-1/corrective-actions/new', '/notes/:noteId/corrective-actions/new')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'حفظ المسودة' }))
    expect(await screen.findByText('العنوان مطلوب.')).toBeInTheDocument()
    expect(screen.getByText('الوصف مطلوب.')).toBeInTheDocument()

    await user.type(screen.getByLabelText('عنوان الإجراء التصحيحي'), 'إجراء جديد')
    await user.type(screen.getByLabelText('وصف الإجراء التصحيحي'), 'وصف الإجراء الجديد')
    await user.click(screen.getByRole('button', { name: 'حفظ المسودة' }))

    await waitFor(() => expect(createAction).toHaveBeenCalledWith('note-1', expect.objectContaining({
      title: 'إجراء جديد',
      description: 'وصف الإجراء الجديد',
      priority: 1,
    })))
  })

  it('submits edit changes and shows 409 reload guidance', async () => {
    currentPermissions.add('CorrectiveActions.Update')
    getAction.mockResolvedValue(action)
    updateAction.mockRejectedValueOnce(new ApiError(409, 'تعارض'))
    updateAction.mockResolvedValueOnce({ id: 'ca-1' })
    renderWithRoute(<CorrectiveActionEditPage />, '/corrective-actions/ca-1/edit', '/corrective-actions/:id/edit')
    const user = userEvent.setup()

    await screen.findByDisplayValue('عنوان قائم')
    await user.clear(screen.getByLabelText('عنوان الإجراء التصحيحي'))
    await user.type(screen.getByLabelText('عنوان الإجراء التصحيحي'), 'عنوان معدل')
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }))
    expect(await screen.findByText('تم تغيير الإجراء بواسطة مستخدم آخر. أعد تحميل الصفحة قبل الحفظ.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }))
    await waitFor(() => expect(updateAction).toHaveBeenLastCalledWith('ca-1', expect.objectContaining({
      title: 'عنوان معدل',
      rowVersion: 'row-v1',
    })))
  })
})
