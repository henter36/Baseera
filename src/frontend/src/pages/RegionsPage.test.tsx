import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { RegionsPage } from '../pages/RegionsPage'

vi.mock('../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Organization.View',
}))

vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual<typeof import('@tanstack/react-query')>('@tanstack/react-query')
  return {
    ...actual,
    useQuery: () => ({
      isLoading: false,
      isError: false,
      data: {
        items: [{ id: '1', code: 'RG-A', nameAr: 'منطقة أ', isActive: true, createdAtUtc: '', rowVersion: '' }],
        page: 1,
        pageSize: 50,
        totalCount: 1,
      },
    }),
  }
})

describe('RegionsPage', () => {
  it('renders RTL region table when permitted', () => {
    render(
      <MemoryRouter>
        <RegionsPage />
      </MemoryRouter>,
    )
    expect(document.documentElement.dir || 'rtl').toBeTruthy()
    expect(screen.getByText('المناطق')).toBeInTheDocument()
    expect(screen.getByText('منطقة أ')).toBeInTheDocument()
  })
})
