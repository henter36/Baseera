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

  function deferred<T>() {
    let resolve!: (value: T) => void
    let reject!: (reason?: unknown) => void
    const promise = new Promise<T>((res, rej) => {
      resolve = res
      reject = rej
    })
    return { promise, resolve, reject }
  }

  const notification = (id: string, titleAr: string, status = 0) => ({
    id,
    targetType: 0,
    targetId: 'target',
    targetReferenceNumber: `OBS-${id}`,
    titleAr,
    messageAr: 'رسالة',
    priority: 2,
    status,
    createdAtUtc: '2026-07-20T00:00:00Z',
    rowVersion: `rv-${id}`,
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
      items: [notification('n1', 'تصعيد')],
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
    expect(markRead).toHaveBeenCalledWith('n1', { rowVersion: 'rv-n1' })

    await userEvent.click(screen.getByRole('button', { name: 'أرشفة' }))
    expect(archive).toHaveBeenCalledWith('n1', { rowVersion: 'rv-n1' })

    await userEvent.click(screen.getByRole('button', { name: 'تعليم الكل كمقروء' }))
    expect(markAllRead).toHaveBeenCalled()
  })

  it('ignores stale list responses when the status filter changes quickly', async () => {
    const first = deferred<{ items: ReturnType<typeof notification>[], page: number, pageSize: number, totalCount: number }>()
    const second = deferred<{ items: ReturnType<typeof notification>[], page: number, pageSize: number, totalCount: number }>()
    list.mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise)

    render(<NotificationsPage />)
    await userEvent.selectOptions(screen.getByRole('combobox'), '1')
    second.resolve({ items: [notification('new', 'الطلب الجديد', 1)], page: 1, pageSize: 20, totalCount: 1 })
    expect(await screen.findByText('الطلب الجديد')).toBeInTheDocument()

    first.resolve({ items: [notification('old', 'الطلب القديم')], page: 1, pageSize: 20, totalCount: 1 })
    await waitFor(() => expect(screen.queryByText('الطلب القديم')).not.toBeInTheDocument())
    expect(screen.getByText('الطلب الجديد')).toBeInTheDocument()
  })

  it('does not update state after unmounting before a list request completes', async () => {
    const request = deferred<{ items: ReturnType<typeof notification>[], page: number, pageSize: number, totalCount: number }>()
    list.mockReturnValue(request.promise)
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined)

    const view = render(<NotificationsPage />)
    view.unmount()
    request.resolve({ items: [notification('late', 'متأخر')], page: 1, pageSize: 20, totalCount: 1 })
    await Promise.resolve()

    expect(consoleError).not.toHaveBeenCalled()
    consoleError.mockRestore()
  })

  it('reloads and notifies the shell after read and archive mutations', async () => {
    list.mockResolvedValue({ items: [notification('n2', 'تصعيد ثان')], page: 1, pageSize: 20, totalCount: 1 })
    markRead.mockResolvedValue({})
    archive.mockResolvedValue({})
    const changed = vi.fn()
    window.addEventListener('baseera:notifications-changed', changed)

    render(<NotificationsPage />)
    expect(await screen.findByText('تصعيد ثان')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'مقروء' }))
    await userEvent.click(screen.getByRole('button', { name: 'أرشفة' }))

    await waitFor(() => expect(list).toHaveBeenCalledTimes(3))
    expect(changed).toHaveBeenCalledTimes(2)
    window.removeEventListener('baseera:notifications-changed', changed)
  })

  it('shows mutation errors and disables buttons while pending', async () => {
    const pending = deferred<object>()
    list.mockResolvedValue({ items: [notification('n3', 'تصعيد ثالث')], page: 1, pageSize: 20, totalCount: 1 })
    markRead.mockReturnValue(pending.promise)

    render(<NotificationsPage />)
    expect(await screen.findByText('تصعيد ثالث')).toBeInTheDocument()
    const read = screen.getByRole('button', { name: 'مقروء' })
    await userEvent.click(read)
    expect(read).toBeDisabled()

    pending.reject(new Error('failed'))
    expect(await screen.findByText('تعذر تنفيذ العملية.')).toBeInTheDocument()
  })
})
