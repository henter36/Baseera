import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { NotificationsPage } from './NotificationsPage'

const { list, markRead, markAllRead, archive } = vi.hoisted(() => ({
  list: vi.fn(),
  markRead: vi.fn(),
  markAllRead: vi.fn(),
  archive: vi.fn(),
}))

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      notifications: {
        list,
        markRead,
        markAllRead,
        archive,
      },
    },
  }
})

describe('NotificationsPage', () => {
  beforeEach(() => {
    list.mockReset()
    markRead.mockReset()
    markAllRead.mockReset()
    archive.mockReset()
  })

  it('shows empty state and retry filter request', async () => {
    list.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    render(<NotificationsPage />)

    expect(await screen.findByText('لا توجد إشعارات.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'إعادة المحاولة' }))
    await waitFor(() => expect(list).toHaveBeenCalledTimes(2))
  })

  it('marks read, marks all read, and archives', async () => {
    list.mockResolvedValue({
      items: [{
        id: 'n1',
        targetType: 0,
        targetId: 'target',
        targetReferenceNumber: 'OBS-1',
        titleAr: 'تصعيد',
        messageAr: 'رسالة',
        priority: 2,
        status: 0,
        createdAtUtc: '2026-07-20T00:00:00Z',
        rowVersion: 'rv',
      }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    })
    markRead.mockResolvedValue({})
    markAllRead.mockResolvedValue({ count: 1 })
    archive.mockResolvedValue({})
    render(<NotificationsPage />)

    expect(await screen.findByText('تصعيد')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'مقروء' }))
    expect(markRead).toHaveBeenCalledWith('n1', { rowVersion: 'rv' })

    await userEvent.click(screen.getByRole('button', { name: 'أرشفة' }))
    expect(archive).toHaveBeenCalledWith('n1', { rowVersion: 'rv' })

    await userEvent.click(screen.getByRole('button', { name: 'تعليم الكل كمقروء' }))
    expect(markAllRead).toHaveBeenCalled()
  })
})
