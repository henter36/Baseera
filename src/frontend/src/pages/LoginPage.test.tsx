import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { LoginPage } from './LoginPage'

const loginEntra = vi.fn(async () => undefined)
const loginTest = vi.fn(async () => undefined)
const logout = vi.fn(async () => undefined)

vi.mock('../auth/AuthProvider', () => ({
  useAuth: () => ({
    isAuthenticated: false,
    loginTest,
    loginEntra,
    logout,
    loading: false,
    error: null,
    configError: null,
    me: null,
    hasPermission: () => false,
    refresh: async () => undefined,
  }),
}))

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return {
    ...actual,
    getAuthMode: () => 'entra' as const,
    isTestAuthAllowed: () => false,
    getTestSubject: () => '',
  }
})

describe('LoginPage Entra', () => {
  beforeEach(() => {
    loginEntra.mockClear()
  })

  it('shows real Entra login action and invokes loginEntra', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>,
    )
    const button = screen.getByRole('button', { name: /Microsoft Entra ID/i })
    expect(button).toBeInTheDocument()
    await user.click(button)
    expect(loginEntra).toHaveBeenCalledTimes(1)
  })
})
