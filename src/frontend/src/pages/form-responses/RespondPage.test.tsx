import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { RespondPage } from './RespondPage'

const { getAssignmentResponse, saveDraft, submit } = vi.hoisted(() => ({
  getAssignmentResponse: vi.fn(),
  saveDraft: vi.fn(),
  submit: vi.fn(),
}))

vi.mock('../../api/client', () => ({
  api: {
    formResponses: {
      getAssignmentResponse,
      saveDraft,
      submit,
    },
  },
  ApiError: class ApiError extends Error {
    status: number
    constructor(status: number, message: string) {
      super(message)
      this.status = status
    }
  },
}))

const baseDetail = {
  assignmentId: 'a1',
  campaignId: 'c1',
  campaignCode: 'C',
  campaignNameAr: 'حملة اختبار',
  cycleId: 'cy1',
  occurrenceKey: '1',
  facilityId: 'f1',
  facilityNameAr: 'سجن أ',
  regionId: 'r1',
  regionNameAr: 'منطقة',
  openAtUtc: new Date().toISOString(),
  dueAtUtc: new Date().toISOString(),
  graceEndsAtUtc: new Date().toISOString(),
  closeAtUtc: new Date(Date.now() + 86400000).toISOString(),
  effectiveDueAtUtc: new Date().toISOString(),
  responseId: 'resp1',
  responseStatus: 0,
  workStatus: 1,
  isOverdue: false,
  isCompleted: false,
  draftVersion: 1,
  draftAnswersJson: JSON.stringify({ q1: 'أصل' }),
  schemaJson: JSON.stringify({
    pages: [{
      key: 'p1',
      titleAr: 'صفحة',
      sections: [{
        key: 's1',
        titleAr: 'قسم',
        fields: [{ key: 'q1', type: 0, labelAr: 'سؤال', isRequired: true }],
      }],
    }],
  }),
  schemaHash: 'hash1',
  classification: 0,
  policy: {
    completionBasis: 1,
    reviewMode: 1,
    requiredApprovalLevels: 1,
    allowLateSubmission: true,
    allowResubmissionAfterReturn: true,
    requireSubmissionAcknowledgement: false,
    requireSeparationOfDuties: true,
  },
  latestSubmission: null,
  visibleComments: [],
  allowedActions: ['SaveDraft', 'Submit'],
  rowVersion: 'rv1',
  fieldVisibility: {},
  fieldRedacted: {},
}

function yesNoDetail() {
  return {
    ...baseDetail,
    draftAnswersJson: JSON.stringify({}),
    schemaJson: JSON.stringify({
      pages: [{
        key: 'p1',
        titleAr: 'صفحة',
        sections: [{
          key: 's1',
          titleAr: 'قسم',
          fields: [{ key: 'yesno', type: 9, labelAr: 'موافق', isRequired: false }],
        }],
      }],
    }),
  }
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const view = render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/form-assignments/a1/respond']}>
        <Routes>
          <Route path="/form-assignments/:assignmentId/respond" element={<RespondPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { client, ...view }
}

describe('RespondPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    getAssignmentResponse.mockResolvedValue({ ...baseDetail })
    saveDraft.mockResolvedValue({
      responseId: 'resp1',
      draftVersion: 2,
      rowVersion: 'rv2',
      lastSavedAtUtc: new Date().toISOString(),
      issues: [],
      calculatedValues: null,
      visibleFieldKeys: ['q1'],
      requiredFieldKeys: ['q1'],
    })
    submit.mockResolvedValue({
      responseId: 'resp1',
      submissionId: 'sub1',
      submissionNumber: 1,
      status: 1,
      rowVersion: 'rv3',
    })
  })

  it('keeps edited value when background refetch arrives while dirty', async () => {
    const { client } = renderPage()
    const input = await screen.findByLabelText('سؤال')
    fireEvent.change(input, { target: { value: 'تعديل محلي' } })

    client.setQueryData(['assignment-response', 'a1'], {
      ...baseDetail,
      draftAnswersJson: JSON.stringify({ q1: 'من الخادم' }),
    })

    await waitFor(() => expect(input).toHaveValue('تعديل محلي'))
  })

  it('does not revert answers after successful autosave when query data is stale', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    renderPage()
    const input = await screen.findByLabelText('سؤال')
    fireEvent.change(input, { target: { value: 'قيمة محفوظة' } })
    await vi.advanceTimersByTimeAsync(900)
    await waitFor(() => expect(saveDraft).toHaveBeenCalled())
    expect(await screen.findByText('تم الحفظ')).toBeInTheDocument()
    expect(input).toHaveValue('قيمة محفوظة')
    vi.useRealTimers()
  })

  it('does not apply server answers while dirty', async () => {
    renderPage()
    const input = await screen.findByLabelText('سؤال')
    await userEvent.type(input, 'X')

    getAssignmentResponse.mockResolvedValue({
      ...baseDetail,
      draftAnswersJson: JSON.stringify({ q1: 'يجب ألا يظهر' }),
    })
    fireEvent.blur(input)

    await waitFor(() => expect(screen.getByText('غير محفوظ')).toBeInTheDocument())
    expect(input).not.toHaveValue('يجب ألا يظهر')
  })

  it('updates versions after successful save', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    renderPage()
    const input = await screen.findByLabelText('سؤال')
    await userEvent.type(input, ' جديد')
    await vi.advanceTimersByTimeAsync(900)
    await waitFor(() => expect(saveDraft).toHaveBeenCalled())
    expect(await screen.findByText('تم الحفظ')).toBeInTheDocument()
    vi.useRealTimers()
  })

  it('shows offline state when save fails without network', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: false })
    renderPage()
    const input = await screen.findByLabelText('سؤال')
    await userEvent.type(input, 'x')
    await vi.advanceTimersByTimeAsync(900)
    expect(await screen.findByText('غير متصل')).toBeInTheDocument()
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: true })
    vi.useRealTimers()
  })

  it('conflict reload applies server snapshot', async () => {
    saveDraft.mockRejectedValue({ status: 409 })
    getAssignmentResponse
      .mockResolvedValueOnce({ ...baseDetail })
      .mockResolvedValueOnce({
        ...baseDetail,
        draftAnswersJson: JSON.stringify({ q1: 'نسخة الخادم' }),
        draftVersion: 5,
        rowVersion: 'rv5',
      })

    vi.useFakeTimers({ shouldAdvanceTime: true })
    renderPage()
    const input = await screen.findByLabelText('سؤال')
    fireEvent.change(input, { target: { value: 'محلي' } })
    await vi.advanceTimersByTimeAsync(900)
    await waitFor(() => expect(saveDraft).toHaveBeenCalled())
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(/تعارض/))
    vi.useRealTimers()

    await userEvent.click(screen.getByRole('button', { name: 'تحميل نسخة الخادم' }))
    await waitFor(() => expect(input).toHaveValue('نسخة الخادم'))
  })

  it('yes/no placeholder stays unanswered and explicit false is preserved', async () => {
    getAssignmentResponse.mockResolvedValue(yesNoDetail())
    renderPage()
    const select = await screen.findByLabelText('موافق')
    expect(select).toHaveValue('')

    await userEvent.selectOptions(select, 'false')
    expect(select).toHaveValue('false')

    await userEvent.selectOptions(select, '')
    expect(select).toHaveValue('')
  })

  it('submit 409 shows conflict UI instead of generic failure', async () => {
    submit.mockRejectedValue({ status: 409 })
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'إرسال' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(/تعارض/)
    expect(screen.queryByText('فشل الإرسال.')).not.toBeInTheDocument()
  })
})
