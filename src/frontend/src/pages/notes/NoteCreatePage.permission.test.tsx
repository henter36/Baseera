import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { NoteCreatePage } from './NoteCreatePage'

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: () => false,
  useAuth: () => ({ me: null }),
}))

describe('NoteCreatePage without Notes.Create', () => {
  it('shows a clear permission-denied message instead of the form', () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <NoteCreatePage />
        </MemoryRouter>
      </QueryClientProvider>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('ليست لديك صلاحية إنشاء ملاحظة.')
  })
})
