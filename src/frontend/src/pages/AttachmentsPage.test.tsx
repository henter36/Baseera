import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AttachmentsPage } from './AttachmentsPage'

const { facilities, uploadAttachment } = vi.hoisted(() => ({
  facilities: vi.fn(),
  uploadAttachment: vi.fn(),
}))

vi.mock('../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Attachments.Upload',
}))

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      facilities,
      uploadAttachment,
    },
  }
})

describe('AttachmentsPage', () => {
  beforeEach(() => {
    facilities.mockReset()
    uploadAttachment.mockReset()
    facilities.mockResolvedValue({
      items: [{ id: 'facility-a', nameAr: 'سجن أ1' }],
    })
    uploadAttachment.mockResolvedValue({ id: 'attachment-a' })
  })

  it('announces upload status with an output element', async () => {
    const user = userEvent.setup()
    const view = renderPage()

    await screen.findByRole('option', { name: 'سجن أ1' })
    await user.selectOptions(screen.getByLabelText('السجن'), 'facility-a')
    await user.upload(screen.getByLabelText('ملف المرفق'), new File(['content'], 'evidence.txt', { type: 'text/plain' }))
    await user.click(screen.getByRole('button', { name: 'رفع' }))

    const statusOutput = view.container.querySelector('output[aria-live="polite"]')
    expect(statusOutput).toHaveTextContent('تم رفع المرفق وتسجيله في التدقيق.')
  })
})

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AttachmentsPage />
    </QueryClientProvider>,
  )
}
