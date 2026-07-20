import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import React from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, type NoteDetail } from '../../api/client'
import { NoteDetailPage } from './NoteDetailPage'

const {
  getNote,
  getHistory,
  getAssignments,
  getAttachments,
  submitNote,
  assignNote,
  cancelNote,
  startWorkNote,
  archiveNote,
  uploadAttachment,
  downloadAttachment,
  listNoteCorrectiveActions,
  currentPermissions,
} = vi.hoisted(() => ({
  getNote: vi.fn(),
  getHistory: vi.fn(),
  getAssignments: vi.fn(),
  getAttachments: vi.fn(),
  submitNote: vi.fn(),
  assignNote: vi.fn(),
  cancelNote: vi.fn(),
  startWorkNote: vi.fn(),
  archiveNote: vi.fn(),
  uploadAttachment: vi.fn(),
  downloadAttachment: vi.fn(),
  listNoteCorrectiveActions: vi.fn(),
  currentPermissions: new Set<string>(['Notes.View']),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => currentPermissions.has(code),
  useAuth: () => ({ hasPermission: (code: string) => currentPermissions.has(code) }),
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      uploadAttachment,
      downloadAttachment,
      notes: {
        ...actual.api.notes,
        get: getNote,
        history: getHistory,
        assignments: getAssignments,
        attachments: getAttachments,
        submit: submitNote,
        assign: assignNote,
        cancel: cancelNote,
        startWork: startWorkNote,
        archive: archiveNote,
        correctiveActions: listNoteCorrectiveActions,
      },
    },
  }
})

const baseNote: NoteDetail = {
  id: 'note-1',
  referenceNumber: 'OBS-00000001',
  title: 'ملاحظة تجريبية',
  description: 'وصف تفصيلي للملاحظة.',
  status: 0,
  statusAr: 'مسودة',
  severity: 2,
  severityAr: 'عالية',
  noteTypeId: '44444444-4444-4444-4444-444444444403',
  noteTypeCode: 'OPERATIONAL',
  noteTypeNameAr: 'تشغيلية',
  noteTypeDescriptionAr: 'ملاحظات تشغيلية',
  noteTypeEntryInstructionsAr: null,
  noteTypeIsActive: true,
  sourceType: 0,
  sourceAr: 'يدوي',
  sourceReference: null,
  classification: 0,
  scopeType: 0,
  reportedByUserId: 'user-1',
  reportedByDisplayName: 'مراقب النظام',
  reportedAtUtc: '2024-01-01T00:00:00Z',
  dueAtUtc: '2020-01-01T00:00:00Z',
  isOverdue: true,
  currentAssignment: null,
  createdAtUtc: '2024-01-01T00:00:00Z',
  rowVersion: 'row-v1',
  isSensitiveRedacted: false,
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/notes/note-1']}>
        <Routes>
          <Route path="/notes/:id" element={<NoteDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('NoteDetailPage', () => {
  beforeEach(() => {
    getNote.mockReset()
    getHistory.mockReset()
    getAssignments.mockReset()
    getAttachments.mockReset()
    submitNote.mockReset()
    assignNote.mockReset()
    cancelNote.mockReset()
    startWorkNote.mockReset()
    archiveNote.mockReset()
    uploadAttachment.mockReset()
    listNoteCorrectiveActions.mockReset()
    listNoteCorrectiveActions.mockResolvedValue({ items: [], page: 1, pageSize: 5, totalCount: 0 })
    currentPermissions.clear()
    currentPermissions.add('Notes.View')
    getHistory.mockResolvedValue([])
    getAssignments.mockResolvedValue([])
    getAttachments.mockResolvedValue([])
  })

  it('shows a loading indicator while fetching', () => {
    getNote.mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText('جاري التحميل…')).toBeInTheDocument()
  })

  it('shows a clear 403 message and does not render the note', async () => {
    getNote.mockRejectedValue(new ApiError(403, 'ممنوع'))
    renderPage()
    expect(await screen.findByText('ليست لديك صلاحية عرض هذه الملاحظة.')).toBeInTheDocument()
  })

  it('shows a clear 404 message for out-of-scope/missing notes', async () => {
    getNote.mockRejectedValue(new ApiError(404, 'غير موجود'))
    renderPage()
    expect(await screen.findByText('الملاحظة غير موجودة أو خارج نطاقك.')).toBeInTheDocument()
  })

  it('renders core fields, badges, and the overdue indicator', async () => {
    getNote.mockResolvedValue(baseNote)
    renderPage()

    expect(await screen.findByText('OBS-00000001')).toBeInTheDocument()
    expect(screen.getByText('مسودة')).toBeInTheDocument()
    expect(screen.getByText('عالية')).toBeInTheDocument()
    expect(screen.getByText('متأخرة')).toBeInTheDocument()
    expect(screen.getByText('وصف تفصيلي للملاحظة.')).toBeInTheDocument()
  })

  it('shows the sensitive-content notice when isSensitiveRedacted is true', async () => {
    getNote.mockResolvedValue({ ...baseNote, title: '[محجوب]', description: '[محتوى حساس — يتطلب صلاحية عرض]', isSensitiveRedacted: true })
    renderPage()

    expect(await screen.findByText(/محجوب لأنه يتطلب صلاحية/)).toBeInTheDocument()
    expect(screen.getByText('[محتوى حساس — يتطلب صلاحية عرض]')).toBeInTheDocument()
  })

  it('renders empty states for history and assignments', async () => {
    getNote.mockResolvedValue(baseNote)
    renderPage()
    await screen.findByText('OBS-00000001')

    expect(await screen.findByText('لا توجد تكليفات سابقة.')).toBeInTheDocument()
    expect(screen.getByText('لا توجد أحداث بعد.')).toBeInTheDocument()
    expect(screen.getByText('لا يوجد تكليف حالي.')).toBeInTheDocument()
  })

  it('only shows action buttons for permissions the user actually has', async () => {
    getNote.mockResolvedValue(baseNote) // Draft status -> submit + cancel candidates
    currentPermissions.add('Notes.Update') // submit requires Notes.Update
    renderPage()
    await screen.findByText('OBS-00000001')

    expect(await screen.findByRole('button', { name: 'إرسال (فتح الملاحظة)' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'إلغاء' })).not.toBeInTheDocument()
  })

  it('shows no action buttons and no archive button without any workflow permission', async () => {
    getNote.mockResolvedValue(baseNote)
    renderPage()
    await screen.findByText('OBS-00000001')

    expect(screen.queryByText('الإجراءات المتاحة')).not.toBeInTheDocument()
  })

  it('runs a transition after reason confirmation and refreshes the note', async () => {
    getNote.mockResolvedValue(baseNote)
    submitNote.mockResolvedValue({ ...baseNote, status: 1, statusAr: 'مفتوحة' })
    currentPermissions.add('Notes.Update')
    renderPage()
    await screen.findByText('OBS-00000001')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'إرسال (فتح الملاحظة)' }))
    await user.click(screen.getByRole('button', { name: 'تأكيد' }))
    expect(await screen.findByText('السبب مطلوب.')).toBeInTheDocument()
    expect(submitNote).not.toHaveBeenCalled()

    await user.type(screen.getByLabelText('سبب إرسال (فتح الملاحظة)'), 'جاهزة للفتح')
    await user.click(screen.getByRole('button', { name: 'تأكيد' }))

    await waitFor(() => expect(submitNote).toHaveBeenCalledWith('note-1', { reason: 'جاهزة للفتح', rowVersion: 'row-v1' }))
  })

  it('shows a clear concurrency message on 409 and offers a reload path instead of overwriting', async () => {
    getNote.mockResolvedValue(baseNote)
    submitNote.mockRejectedValue(new ApiError(409, 'تعارض'))
    currentPermissions.add('Notes.Update')
    renderPage()
    await screen.findByText('OBS-00000001')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'إرسال (فتح الملاحظة)' }))
    await user.type(screen.getByLabelText('سبب إرسال (فتح الملاحظة)'), 'جاهزة للفتح')
    await user.click(screen.getByRole('button', { name: 'تأكيد' }))

    expect(await screen.findByText(/تم تغيير الملاحظة بواسطة مستخدم آخر/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'إعادة تحميل' })).toBeInTheDocument()
  })

  it('requires exactly one assignee for the assign action', async () => {
    getNote.mockResolvedValue({ ...baseNote, status: 1, statusAr: 'مفتوحة' })
    currentPermissions.add('Notes.Assign')
    renderPage()
    await screen.findByText('OBS-00000001')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'تكليف' }))
    await user.type(screen.getByLabelText('سبب التكليف'), 'تكليف الفريق')
    await user.click(screen.getByRole('button', { name: 'تأكيد' }))

    expect(await screen.findByText('يجب تحديد مستخدم أو إدارة واحدة فقط للتكليف.')).toBeInTheDocument()
    expect(assignNote).not.toHaveBeenCalled()
  })

  it('shows the recorded rowVersion when archiving so it can be used to restore later', async () => {
    getNote.mockResolvedValue(baseNote)
    currentPermissions.add('Notes.Archive')
    renderPage()
    await screen.findByText('OBS-00000001')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'أرشفة' }))
    expect(await screen.findByText('row-v1')).toBeInTheDocument()
  })

  it('lists note attachments from the dedicated endpoint and shows scan status', async () => {
    getNote.mockResolvedValue(baseNote)
    getAttachments.mockResolvedValue([
      {
        id: 'att-1',
        entityType: 'OperationalNote',
        entityId: 'note-1',
        originalFileName: 'report.pdf',
        contentType: 'application/pdf',
        sizeBytes: 100,
        sha256: 'abc',
        classification: 0,
        scanStatus: 1,
        uploadedAtUtc: '2024-01-01T00:00:00Z',
      },
    ])
    downloadAttachment.mockResolvedValue({ blob: new Blob(['x']), fileName: 'report.pdf' })
    renderPage()
    await screen.findByText('OBS-00000001')

    expect(await screen.findByText('report.pdf')).toBeInTheDocument()
    expect(screen.getByText('سليم')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'تنزيل' })).toBeInTheDocument()
    expect(getAttachments).toHaveBeenCalledWith('note-1')

    await userEvent.setup().click(screen.getByRole('button', { name: 'تنزيل' }))
    await waitFor(() => expect(downloadAttachment).toHaveBeenCalledWith('att-1'))
  })

  it('renders the corrective actions summary when permitted', async () => {
    getNote.mockResolvedValue(baseNote)
    listNoteCorrectiveActions.mockResolvedValue({
      items: [
        {
          id: 'ca-1',
          referenceNumber: 'CA-00000001',
          operationalNoteId: 'note-1',
          operationalNoteReferenceNumber: 'OBS-00000001',
          title: 'إجراء مرتبط',
          descriptionSnippet: null,
          priority: 2,
          priorityAr: 'عالية',
          status: 4,
          statusAr: 'بانتظار التحقق',
          classification: 0,
          ownerDepartmentId: null,
          dueAtUtc: '2020-01-01T00:00:00Z',
          isOverdue: true,
          isDueSoon: false,
          overdueDays: 1,
          currentAssigneeDisplay: null,
          createdAtUtc: '2024-01-01T00:00:00Z',
          rowVersion: 'row-ca',
          isSensitiveRedacted: false,
        },
      ],
      page: 1,
      pageSize: 5,
      totalCount: 1,
    })
    currentPermissions.add('CorrectiveActions.View')
    currentPermissions.add('CorrectiveActions.Create')
    renderPage()

    expect(await screen.findByText('الإجراءات التصحيحية')).toBeInTheDocument()
    expect(await screen.findByText('إجراء مرتبط')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'إضافة إجراء' })).toBeInTheDocument()
    expect(listNoteCorrectiveActions).toHaveBeenCalledWith('note-1', { page: 1, pageSize: 5, sortBy: 'createdAtUtc', sortDesc: true })
  })

  it('hides download and shows sensitive permission message for redacted attachments', async () => {
    getNote.mockResolvedValue(baseNote)
    getAttachments.mockResolvedValue([
      {
        id: 'att-secret',
        entityType: 'OperationalNote',
        entityId: 'note-1',
        originalFileName: '[محجوب]',
        contentType: 'application/octet-stream',
        sizeBytes: 0,
        sha256: '',
        classification: 2,
        scanStatus: 1,
        uploadedAtUtc: '2024-01-01T00:00:00Z',
        isSensitiveRedacted: true,
      },
    ])
    renderPage()
    await screen.findByText('OBS-00000001')

    expect(await screen.findByText('مرفق حسّاس — يتطلب صلاحية تنزيل حساسة')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'تنزيل' })).not.toBeInTheDocument()
  })

  it('hides download for quarantined or rejected attachments', async () => {
    getNote.mockResolvedValue(baseNote)
    getAttachments.mockResolvedValue([
      {
        id: 'att-q',
        entityType: 'OperationalNote',
        entityId: 'note-1',
        originalFileName: 'bad.bin',
        contentType: 'application/octet-stream',
        sizeBytes: 10,
        sha256: 'x',
        classification: 0,
        scanStatus: 2,
        uploadedAtUtc: '2024-01-01T00:00:00Z',
      },
      {
        id: 'att-r',
        entityType: 'OperationalNote',
        entityId: 'note-1',
        originalFileName: 'rejected.bin',
        contentType: 'application/octet-stream',
        sizeBytes: 10,
        sha256: 'y',
        classification: 0,
        scanStatus: 3,
        uploadedAtUtc: '2024-01-01T00:00:00Z',
      },
    ])
    renderPage()
    await screen.findByText('OBS-00000001')

    expect(await screen.findAllByText('التنزيل متاح بعد اكتمال الفحص فقط')).toHaveLength(2)
    expect(screen.queryByRole('button', { name: 'تنزيل' })).not.toBeInTheDocument()
  })

  it('only enables the attachment download button once the scan status is Clean, and refetches the list after upload', async () => {
    getNote.mockResolvedValue(baseNote)
    getAttachments.mockResolvedValueOnce([]).mockResolvedValueOnce([
      {
        id: 'att-1',
        entityType: 'OperationalNote',
        entityId: 'note-1',
        originalFileName: 'report.pdf',
        contentType: 'application/pdf',
        sizeBytes: 100,
        sha256: 'abc',
        classification: 0,
        scanStatus: 0,
        uploadedAtUtc: '2024-01-01T00:00:00Z',
      },
    ])
    currentPermissions.add('Attachments.Upload')
    uploadAttachment.mockResolvedValue({
      id: 'att-1',
      entityType: 'OperationalNote',
      entityId: 'note-1',
      originalFileName: 'report.pdf',
      contentType: 'application/pdf',
      sizeBytes: 100,
      sha256: 'abc',
      classification: 0,
      scanStatus: 0,
      uploadedAtUtc: '2024-01-01T00:00:00Z',
    })
    renderPage()
    await screen.findByText('OBS-00000001')
    expect(await screen.findByText('لا توجد مرفقات لهذه الملاحظة.')).toBeInTheDocument()

    const user = userEvent.setup()
    const fileInput = screen.getByLabelText('ملف المرفق')
    const file = new File(['hello'], 'report.pdf', { type: 'application/pdf' })
    await user.upload(fileInput, file)
    await user.click(screen.getByRole('button', { name: 'رفع' }))

    expect(await screen.findByText('قيد الفحص')).toBeInTheDocument()
    expect(screen.getByText('التنزيل متاح بعد اكتمال الفحص فقط')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'تنزيل' })).not.toBeInTheDocument()
    expect(getAttachments).toHaveBeenCalledTimes(2)
  })

  it('calls the notes and attachments APIs exactly once even under React.StrictMode', async () => {
    getNote.mockResolvedValue(baseNote)
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <React.StrictMode>
        <QueryClientProvider client={queryClient}>
          <MemoryRouter initialEntries={['/notes/note-1']}>
            <Routes>
              <Route path="/notes/:id" element={<NoteDetailPage />} />
            </Routes>
          </MemoryRouter>
        </QueryClientProvider>
      </React.StrictMode>,
    )

    await screen.findByText('OBS-00000001')
    await waitFor(() => expect(getHistory).toHaveBeenCalledTimes(1))
    await waitFor(() => expect(getAssignments).toHaveBeenCalledTimes(1))
    await waitFor(() => expect(getAttachments).toHaveBeenCalledTimes(1))
  })
})
