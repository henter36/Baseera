import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FormResponseReviewDetailPage } from './FormResponseReviewsPage'

const { getReview, approve } = vi.hoisted(() => ({
  getReview: vi.fn(),
  approve: vi.fn(),
}))

vi.mock('../../api/client', () => ({
  api: {
    formResponses: {
      getReview,
      startReview: vi.fn(),
      returnResponse: vi.fn(),
      approve,
      reject: vi.fn(),
      close: vi.fn(),
      reviews: vi.fn(),
    },
  },
}))

const reviewDetail = {
  workspace: {
    assignmentId: 'a1',
    campaignId: 'c1',
    campaignCode: 'C',
    campaignNameAr: 'حملة',
    cycleId: 'cy1',
    occurrenceKey: '1',
    facilityId: 'f1',
    facilityNameAr: 'سجن',
    regionId: 'r1',
    regionNameAr: 'منطقة',
    openAtUtc: new Date().toISOString(),
    dueAtUtc: new Date().toISOString(),
    graceEndsAtUtc: new Date().toISOString(),
    closeAtUtc: new Date().toISOString(),
    effectiveDueAtUtc: new Date().toISOString(),
    responseId: 'r1',
    responseStatus: 2,
    workStatus: 3,
    isOverdue: false,
    isCompleted: false,
    draftVersion: 1,
    draftAnswersJson: '{"q1":"x"}',
    schemaJson: '{}',
    schemaHash: 'h',
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
    latestSubmission: { canonicalAnswersJson: '{"q1":"x"}' },
    visibleComments: [],
    allowedActions: ['Approve'],
    rowVersion: 'rv1',
    fieldVisibility: {},
    fieldRedacted: {},
  },
  submissions: [],
  decisions: [],
  comments: [],
  history: [],
}

function renderDetailPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/form-responses/r1/review']}>
        <Routes>
          <Route path="/form-responses/:responseId/review" element={<FormResponseReviewDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('FormResponseReviewDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    getReview.mockResolvedValue(reviewDetail)
    approve.mockResolvedValue(undefined)
  })

  it.each([
    { status: 403, message: /لا يمكن تنفيذ/ },
    { status: 409, message: /تعارض في النسخة/ },
  ])('shows alert on approve error status $status', async ({ status, message }) => {
    approve.mockRejectedValueOnce({ status })
    renderDetailPage()
    await screen.findByRole('button', { name: 'اعتماد' })
    await userEvent.click(screen.getByRole('button', { name: 'اعتماد' }))
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(message))
  })

  it('shows network alert when approve fails generically', async () => {
    approve.mockRejectedValueOnce(new Error('network'))
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: false })
    renderDetailPage()
    await userEvent.click(await screen.findByRole('button', { name: 'اعتماد' }))
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(/انقطع الاتصال/))
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: true })
  })
})
