import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, type CorrectiveActionDetail } from '../../api/client'
import { CorrectiveActionStatus } from '../../correctiveActions/correctiveActionEnums'
import { CorrectiveActionDetailPage } from './CorrectiveActionDetailPage'

const {
  getAction,
  getHistory,
  getAssignments,
  getAttachments,
  submitAction,
  assignAction,
  verifyCompletion,
  downloadAttachment,
  currentPermissions,
} = vi.hoisted(() => ({
  getAction: vi.fn(),
  getHistory: vi.fn(),
  getAssignments: vi.fn(),
  getAttachments: vi.fn(),
  submitAction: vi.fn(),
  assignAction: vi.fn(),
  verifyCompletion: vi.fn(),
  downloadAttachment: vi.fn(),
  currentPermissions: new Set<string>(['CorrectiveActions.View']),
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
      downloadAttachment,
      correctiveActions: {
        ...actual.api.correctiveActions,
        get: getAction,
        history: getHistory,
        assignments: getAssignments,
        attachments: getAttachments,
        submit: submitAction,
        assign: assignAction,
        verifyCompletion,
      },
    },
  }
})

const baseAction: CorrectiveActionDetail = {
  id: 'ca-1',
  referenceNumber: 'CA-00000001',
  operationalNoteId: 'note-1',
  operationalNoteReferenceNumber: 'OBS-00000001',
  title: 'إجراء تصحيحي',
  description: 'وصف الإجراء',
  priority: 1,
  priorityAr: 'متوسطة',
  status: CorrectiveActionStatus.Draft,
  statusAr: 'مسودة',
  classification: 0,
  ownerDepartmentId: null,
  createdByUserId: 'user-1',
  createdByDisplayName: 'منشئ',
  createdAtUtc: '2024-01-01T00:00:00Z',
  submittedAtUtc: null,
  workStartedAtUtc: null,
  submittedForVerificationAtUtc: null,
  completedAtUtc: null,
  completedByUserId: null,
  completionSummary: null,
  reopenedAtUtc: null,
  reopenReason: null,
  cancelledAtUtc: null,
  cancelReason: null,
  dueAtUtc: null,
  isOverdue: false,
  overdueDays: null,
  currentAssignment: null,
  rowVersion: 'row-v1',
  isSensitiveRedacted: false,
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/corrective-actions/ca-1']}>
        <Routes>
          <Route path="/corrective-actions/:id" element={<CorrectiveActionDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('CorrectiveActionDetailPage', () => {
  beforeEach(() => {
    getAction.mockReset()
    getHistory.mockReset()
    getAssignments.mockReset()
    getAttachments.mockReset()
    submitAction.mockReset()
    assignAction.mockReset()
    verifyCompletion.mockReset()
    downloadAttachment.mockReset()
    currentPermissions.clear()
    currentPermissions.add('CorrectiveActions.View')
    getHistory.mockResolvedValue([])
    getAssignments.mockResolvedValue([])
    getAttachments.mockResolvedValue([])
  })

  it('shows action buttons based on permission and status', async () => {
    getAction.mockResolvedValue(baseAction)
    currentPermissions.add('CorrectiveActions.Create')
    renderPage()

    expect(await screen.findByRole('button', { name: 'فتح الإجراء' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'إلغاء' })).not.toBeInTheDocument()
  })

  it('validates reason, completion summary, and assignment XOR', async () => {
    getAction.mockResolvedValue({ ...baseAction, status: CorrectiveActionStatus.Open, statusAr: 'مفتوح' })
    currentPermissions.add('CorrectiveActions.Assign')
    renderPage()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'تكليف / إعادة تكليف' }))
    await user.click(screen.getByRole('button', { name: 'تأكيد' }))
    expect(await screen.findByText('السبب مطلوب.')).toBeInTheDocument()

    await user.type(screen.getByLabelText('سبب تكليف / إعادة تكليف'), 'سبب')
    await user.click(screen.getByRole('button', { name: 'تأكيد' }))
    expect(await screen.findByText('يجب تحديد مستخدم أو إدارة واحدة فقط.')).toBeInTheDocument()

    const assignedUser = screen.getByLabelText('معرف المستخدم المكلّف')
    const assignedDepartment = screen.getByLabelText('معرف الإدارة المكلّفة')
    await user.type(assignedUser, 'user-1')
    await user.type(assignedDepartment, 'dept-1')
    expect(assignedUser).toHaveValue('')
    expect(assignedDepartment).toHaveValue('dept-1')

    cleanup()
    getAction.mockResolvedValue({ ...baseAction, status: CorrectiveActionStatus.PendingVerification, statusAr: 'بانتظار التحقق' })
    currentPermissions.clear()
    currentPermissions.add('CorrectiveActions.View')
    currentPermissions.add('CorrectiveActions.VerifyCompletion')
    renderPage()
    await user.click(await screen.findByRole('button', { name: 'اعتماد الإنجاز' }))
    await user.type(screen.getByLabelText('سبب اعتماد الإنجاز'), 'سبب')
    await user.click(screen.getByRole('button', { name: 'تأكيد' }))
    expect(await screen.findByText('ملخص الإنجاز مطلوب.')).toBeInTheDocument()
  })

  it('handles 403, 404, and 409 with reload action', async () => {
    getAction.mockRejectedValueOnce(new ApiError(403, 'ممنوع'))
    renderPage()
    expect(await screen.findByText('ليست لديك صلاحية عرض هذا الإجراء.')).toBeInTheDocument()

    getAction.mockRejectedValueOnce(new ApiError(404, 'غير موجود'))
    renderPage()
    expect(await screen.findByText('الإجراء غير موجود أو خارج نطاقك.')).toBeInTheDocument()

    getAction.mockResolvedValue(baseAction)
    submitAction.mockRejectedValue(new ApiError(409, 'تعارض'))
    currentPermissions.add('CorrectiveActions.Create')
    renderPage()
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'فتح الإجراء' }))
    await user.type(screen.getByLabelText('سبب فتح الإجراء'), 'سبب')
    await user.click(screen.getByRole('button', { name: 'تأكيد' }))

    expect(await screen.findByText(/حدث تعارض/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'إعادة تحميل' })).toBeInTheDocument()
  })

  it('executes a successful transition', async () => {
    getAction.mockResolvedValue(baseAction)
    submitAction.mockResolvedValue({ ...baseAction, status: CorrectiveActionStatus.Open, statusAr: 'مفتوح' })
    currentPermissions.add('CorrectiveActions.Create')
    renderPage()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'فتح الإجراء' }))
    await user.type(screen.getByLabelText('سبب فتح الإجراء'), 'فتح')
    await user.click(screen.getByRole('button', { name: 'تأكيد' }))

    await waitFor(() => expect(submitAction).toHaveBeenCalledWith('ca-1', { reason: 'فتح', rowVersion: 'row-v1' }))
  })

  it('hides download for PendingScan and allows Clean download', async () => {
    getAction.mockResolvedValue(baseAction)
    getAttachments.mockResolvedValue([
      {
        id: 'att-pending',
        entityType: 'correctiveaction',
        entityId: 'ca-1',
        originalFileName: 'pending.txt',
        contentType: 'text/plain',
        sizeBytes: 10,
        sha256: 'abc',
        classification: 0,
        scanStatus: 0,
        uploadedAtUtc: '2024-01-01T00:00:00Z',
      },
      {
        id: 'att-clean',
        entityType: 'correctiveaction',
        entityId: 'ca-1',
        originalFileName: 'clean.txt',
        contentType: 'text/plain',
        sizeBytes: 10,
        sha256: 'def',
        classification: 0,
        scanStatus: 1,
        uploadedAtUtc: '2024-01-01T00:00:00Z',
      },
    ])
    downloadAttachment.mockResolvedValue({ blob: new Blob(['ok']), fileName: 'clean.txt' })
    renderPage()
    const user = userEvent.setup()

    expect(await screen.findByText('pending.txt')).toBeInTheDocument()
    expect(screen.getByText('لا يمكن تنزيل المرفق قبل حالة الفحص السليمة.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'تنزيل' }))
    expect(downloadAttachment).toHaveBeenCalledWith('att-clean')
  })
})
