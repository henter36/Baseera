import { fireEvent, render, screen } from '@testing-library/react'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { createEmptySchema, type FormSchemaDocument } from '../../../forms/designer/schemaTypes'
import { StudioMobileReview } from './StudioMobileReview'

function Harness({ schema, setSchema }: Readonly<{ schema: FormSchemaDocument; setSchema: (s: FormSchemaDocument) => void }>) {
  return (
    <StudioMobileReview
      schema={schema}
      errorCount={0}
      warningCount={0}
      onRenameFieldLabel={(pageId, fieldId, label) => {
        setSchema({
          ...schema,
          pages: schema.pages.map((p) => p.id !== pageId ? p : {
            ...p,
            sections: p.sections.map((s) => ({ ...s, fields: s.fields.map((f) => f.id === fieldId ? { ...f, labelAr: label } : f) })),
          }),
        })
      }}
      onRenamePageTitle={(pageId, title) => {
        setSchema({ ...schema, pages: schema.pages.map((p) => p.id !== pageId ? p : { ...p, titleAr: title }) })
      }}
      onTogglePreview={() => undefined}
      canRequestReview={false}
      onRequestReview={() => undefined}
      isRequestingReview={false}
    />
  )
}

function StatefulHarness({ initial }: Readonly<{ initial: FormSchemaDocument }>) {
  const [schema, setSchema] = useState(initial)
  return <Harness schema={schema} setSchema={setSchema} />
}

describe('StudioMobileReview', () => {
  it('renders the narrow-screen banner as an accessible status message', () => {
    render(<StatefulHarness initial={createEmptySchema()} />)
    const banner = screen.getByRole('status')
    expect(banner.tagName.toLowerCase()).toBe('output')
    expect(banner).toHaveTextContent(/الهيكلة المتقدمة/)
  })

  it('resyncs the page title draft when the underlying schema changes externally', () => {
    const schema = createEmptySchema()
    const { rerender } = render(<Harness schema={schema} setSchema={() => undefined} />)
    const titleInput = screen.getByLabelText('عنوان الصفحة') as HTMLInputElement
    expect(titleInput).toHaveValue('الصفحة 1')

    const reloaded = { ...schema, pages: [{ ...schema.pages[0], titleAr: 'عنوان محدَّث من الخادم' }] }
    rerender(<Harness schema={reloaded} setSchema={() => undefined} />)
    expect(titleInput).toHaveValue('عنوان محدَّث من الخادم')
  })

  it('commits the freshly reloaded value on blur, not a stale draft, after an external reload', () => {
    const onRenamePageTitle = vi.fn()
    const schema = createEmptySchema()
    const props = {
      errorCount: 0,
      warningCount: 0,
      onRenameFieldLabel: () => undefined,
      onRenamePageTitle,
      onTogglePreview: () => undefined,
      canRequestReview: false,
      onRequestReview: () => undefined,
      isRequestingReview: false,
    }
    const { rerender } = render(<StudioMobileReview schema={schema} {...props} />)
    const titleInput = screen.getByLabelText('عنوان الصفحة') as HTMLInputElement

    const reloaded = { ...schema, pages: [{ ...schema.pages[0], titleAr: 'عنوان من نسخة أحدث' }] }
    rerender(<StudioMobileReview schema={reloaded} {...props} />)
    fireEvent.blur(titleInput)

    expect(onRenamePageTitle).toHaveBeenCalledWith(schema.pages[0].id, 'عنوان من نسخة أحدث')
  })

  it('resyncs a field label draft when the underlying schema changes externally', () => {
    const schema = createEmptySchema()
    const { rerender } = render(<Harness schema={schema} setSchema={() => undefined} />)
    const labelInput = screen.getByLabelText('عنوان الحقل') as HTMLInputElement
    expect(labelInput).toHaveValue('حقل نصي')

    const field = schema.pages[0].sections[0].fields[0]
    const reloaded = {
      ...schema,
      pages: [{
        ...schema.pages[0],
        sections: [{ ...schema.pages[0].sections[0], fields: [{ ...field, labelAr: 'تسمية محدَّثة من الخادم' }] }],
      }],
    }
    rerender(<Harness schema={reloaded} setSchema={() => undefined} />)
    expect(labelInput).toHaveValue('تسمية محدَّثة من الخادم')
  })
})
