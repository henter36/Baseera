import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { NotesListPage } from './NotesListPage'

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: () => false,
}))

describe('NotesListPage without Notes.View', () => {
  it('shows a clear permission-denied message instead of the list', () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <NotesListPage />
        </MemoryRouter>
      </QueryClientProvider>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('ليست لديك صلاحية عرض الملاحظات.')
  })
})
