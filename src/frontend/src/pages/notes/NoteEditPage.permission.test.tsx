import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { NoteEditPage } from './NoteEditPage'

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: () => false,
}))

describe('NoteEditPage without Notes.Update', () => {
  it('shows a clear permission-denied message instead of the form', () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/notes/note-1/edit']}>
          <Routes>
            <Route path="/notes/:id/edit" element={<NoteEditPage />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('ليست لديك صلاحية تعديل الملاحظات.')
  })
})
