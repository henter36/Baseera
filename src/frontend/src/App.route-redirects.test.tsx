import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { describe, expect, it } from 'vitest'
import { RedirectToStudioEdit, RedirectToStudioNew } from './App'

function TargetProbe({ label }: Readonly<{ label: string }>) {
  return <div>{label}</div>
}

describe('legacy form designer route redirects', () => {
  it('redirects /forms/:formId/versions/:versionId/edit deep links into the studio with versionId preserved', () => {
    render(
      <MemoryRouter initialEntries={['/forms/f1/versions/v1/edit']}>
        <Routes>
          <Route path="/forms/:formId/versions/:versionId/edit" element={<RedirectToStudioEdit />} />
          <Route path="/forms/designer/:formId" element={<TargetProbe label="landed-in-studio" />} />
        </Routes>
      </MemoryRouter>,
    )
    expect(screen.getByText('landed-in-studio')).toBeInTheDocument()
  })

  it('redirects the dead /forms/:formId/versions/new route into the studio for that form', () => {
    render(
      <MemoryRouter initialEntries={['/forms/f1/versions/new']}>
        <Routes>
          <Route path="/forms/:formId/versions/new" element={<RedirectToStudioNew />} />
          <Route path="/forms/designer/:formId" element={<TargetProbe label="landed-in-studio" />} />
        </Routes>
      </MemoryRouter>,
    )
    expect(screen.getByText('landed-in-studio')).toBeInTheDocument()
  })
})
