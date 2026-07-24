import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FacilitiesPage } from './FacilitiesPage'

const { getFacilities, currentPermissions } = vi.hoisted(() => ({
  getFacilities: vi.fn(),
  currentPermissions: new Set<string>(),
}))

vi.mock('../auth/AuthProvider', () => ({
  usePermission: (code: string) => currentPermissions.has(code),
}))

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      facilities: getFacilities,
    },
  }
})

describe('FacilitiesPage', () => {
  beforeEach(() => {
    getFacilities.mockReset()
    getFacilities.mockResolvedValue({
      items: [{
        id: 'facility-a',
        code: 'FA',
        nameAr: 'سجن أ1',
        facilityType: 'سجن',
        isActive: true,
      }],
    })
    currentPermissions.clear()
    currentPermissions.add('Organization.View')
    currentPermissions.add('Workspaces.View')
    currentPermissions.add('Workspaces.ViewFacility')
  })

  it('renders facility workspace links when both workspace permissions exist', async () => {
    renderPage()

    expect(await screen.findByRole('link', { name: 'مركز القرار' })).toHaveAttribute(
      'href',
      '/workspaces/facilities/facility-a',
    )
  })

  it('hides facility workspace links when only facility workspace permission exists', async () => {
    currentPermissions.delete('Workspaces.View')

    renderPage()

    expect(await screen.findByText('سجن أ1')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'مركز القرار' })).not.toBeInTheDocument()
  })

  it('hides facility workspace links when only general workspace permission exists', async () => {
    currentPermissions.delete('Workspaces.ViewFacility')

    renderPage()

    expect(await screen.findByText('سجن أ1')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'مركز القرار' })).not.toBeInTheDocument()
  })
})

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <FacilitiesPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}
