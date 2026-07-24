import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { OperationalDashboardPage } from './OperationalDashboardPage'

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: () => false,
}))

describe('OperationalDashboardPage permission', () => {
  it('shows permission denial when dashboard permissions are missing', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter>
          <OperationalDashboardPage />
        </MemoryRouter>
      </QueryClientProvider>,
    )
    expect(screen.getByRole('alert')).toHaveTextContent('ليست لديك صلاحية عرض لوحة المتابعة التشغيلية.')
  })
})
