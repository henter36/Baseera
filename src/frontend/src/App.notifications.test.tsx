import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const { unreadCount } = vi.hoisted(() => ({
  unreadCount: vi.fn(),
}))

vi.mock('./auth/AuthProvider', () => ({
  useAuth: () => ({
    me: { displayNameAr: 'مستخدم', permissions: ['Notifications.ViewOwn'] },
    logout: vi.fn(),
    loading: false,
    isAuthenticated: true,
    hasPermission: (code: string) => code === 'Notifications.ViewOwn',
  }),
}))

vi.mock('./api/client', async () => {
  const actual = await vi.importActual<typeof import('./api/client')>('./api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      notifications: {
        unreadCount,
        list: vi.fn().mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
      },
    },
  }
})

describe('App notification shell', () => {
  beforeEach(() => {
    unreadCount.mockReset()
    unreadCount.mockResolvedValue({ count: 3 })
  })

  it('shows unread count for users with notification permission', async () => {
    render(
      <MemoryRouter initialEntries={['/notifications']}>
        <App />
      </MemoryRouter>,
    )

    await waitFor(() => expect(unreadCount).toHaveBeenCalled())
    expect(screen.getByText('الإشعارات (3)')).toBeInTheDocument()
  })
})
