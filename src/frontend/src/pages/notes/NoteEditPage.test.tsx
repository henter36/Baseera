import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, type NoteDetail } from '../../api/client'
import { NoteEditPage } from './NoteEditPage'

const { getNote, updateNote, listDepartments, navigateMock } = vi.hoisted(() => ({
  getNote: vi.fn(),
  updateNote: vi.fn(),
  listDepartments: vi.fn(async () => ({
    items: [{ id: 'dept-1', organizationId: 'org-1', code: 'D-1', nameAr: 'إدارة الصيانة', isActive: true }],
    page: 1,
    pageSize: 100,
    totalCount: 1,
  })),
  navigateMock: vi.fn(),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Notes.Update',
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
      departments: listDepartments,
      notes: { ...actual.api.notes, get: getNote, update: updateNote },
    },
  }
})

const baseNote: NoteDetail = {
  id: 'note-1',
  referenceNumber: 'OBS-00000001',
  title: 'عنوان أصلي',
  description: 'وصف أصلي كافٍ.',
  status: 1,
  statusAr: 'مفتوحة',
  severity: 1,
  severityAr: 'متوسطة',
  category: 0,
  categoryAr: 'أمنية',
  sourceType: 0,
  sourceAr: 'يدوي',
  sourceReference: null,
  classification: 0,
  scopeType: 0,
  reportedByUserId: 'user-1',
  reportedAtUtc: '2024-01-01T00:00:00Z',
  isOverdue: false,
  currentAssignment: null,
  createdAtUtc: '2024-01-01T00:00:00Z',
  rowVersion: 'row-v1',
  isSensitiveRedacted: false,
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/notes/note-1/edit']}>
        <Routes>
          <Route path="/notes/:id/edit" element={<NoteEditPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('NoteEditPage', () => {
  beforeEach(() => {
    getNote.mockReset()
    updateNote.mockReset()
    navigateMock.mockReset()
  })

  it('does not render scope or status fields', async () => {
    getNote.mockResolvedValue(baseNote)
    renderPage()

    await screen.findByDisplayValue('عنوان أصلي')
    expect(screen.queryByLabelText('نطاق الملاحظة')).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/الحالة/)).not.toBeInTheDocument()
  })

  it('shows validation errors for an empty title/description', async () => {
    getNote.mockResolvedValue(baseNote)
    renderPage()
    await screen.findByDisplayValue('عنوان أصلي')

    const user = userEvent.setup()
    const titleInput = screen.getByLabelText('العنوان')
    await user.clear(titleInput)
    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }))

    expect(await screen.findByText('العنوان مطلوب.')).toBeInTheDocument()
    expect(updateNote).not.toHaveBeenCalled()
  })

  it('sends the loaded rowVersion and navigates to the detail page on success', async () => {
    getNote.mockResolvedValue(baseNote)
    updateNote.mockResolvedValue({ ...baseNote, title: 'عنوان محدث' })
    renderPage()
    await screen.findByDisplayValue('عنوان أصلي')

    const user = userEvent.setup()
    await user.clear(screen.getByLabelText('العنوان'))
    await user.type(screen.getByLabelText('العنوان'), 'عنوان محدث')
    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }))

    await waitFor(() => expect(updateNote).toHaveBeenCalledTimes(1))
    expect(updateNote).toHaveBeenCalledWith('note-1', expect.objectContaining({ rowVersion: 'row-v1', title: 'عنوان محدث' }))
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/notes/note-1'))
  })

  it('shows a clear conflict message on 409 and blocks resubmission until reload', async () => {
    getNote.mockResolvedValue(baseNote)
    updateNote.mockRejectedValue(new ApiError(409, 'تعارض'))
    renderPage()
    await screen.findByDisplayValue('عنوان أصلي')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }))

    expect(await screen.findByText(/تم تعديل الملاحظة من مستخدم آخر/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'حفظ التعديلات' })).toBeDisabled()
  })

  it('shows a clear message for out-of-scope/not-found notes (404)', async () => {
    getNote.mockRejectedValue(new ApiError(404, 'غير موجود'))
    renderPage()

    expect(await screen.findByText('الملاحظة غير موجودة أو خارج نطاقك.')).toBeInTheDocument()
  })
})
