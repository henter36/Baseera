import { useState } from 'react'
import type { FormSchemaDocument } from '../../../forms/designer/schemaTypes'

type StudioOutlineProps = {
  schema: FormSchemaDocument
  selectedFieldId: string | null
  errorCountByPageId: (pageId: string) => number
  onSelectField: (pageId: string, fieldId: string) => void
}

export function StudioOutline({ schema, selectedFieldId, errorCountByPageId, onSelectField }: Readonly<StudioOutlineProps>) {
  const [collapsedPages, setCollapsedPages] = useState<Set<string>>(new Set())

  const togglePage = (pageId: string) => {
    setCollapsedPages((prev) => {
      const next = new Set(prev)
      if (next.has(pageId)) next.delete(pageId)
      else next.add(pageId)
      return next
    })
  }

  return (
    <nav className="studio-outline" aria-label="مخطط النموذج">
      {schema.pages.map((page) => {
        const fieldCount = page.sections.reduce((n, s) => n + s.fields.length, 0)
        const errorCount = errorCountByPageId(page.id)
        const collapsed = collapsedPages.has(page.id)
        return (
          <div className="studio-outline-page" key={page.id}>
            <button type="button" className="secondary" aria-expanded={!collapsed} onClick={() => togglePage(page.id)}>
              {collapsed ? '▸' : '▾'} {page.titleAr}
              {' '}<span className="studio-outline-count">({fieldCount} حقل)</span>
              {errorCount > 0 && <span className="studio-outline-error-count"> — {errorCount} خطأ</span>}
            </button>
            {!collapsed && page.sections.map((section) => (
              <div className="studio-outline-section" key={section.id}>
                <div className="muted">{section.titleAr}</div>
                {section.fields.length === 0 ? (
                  <div className="studio-outline-field muted">لا حقول</div>
                ) : (
                  section.fields.map((field) => (
                    <button
                      key={field.id}
                      type="button"
                      className={field.id === selectedFieldId ? 'studio-outline-field' : 'studio-outline-field secondary'}
                      onClick={() => onSelectField(page.id, field.id)}
                    >
                      {field.labelAr}
                    </button>
                  ))
                )}
              </div>
            ))}
          </div>
        )
      })}
    </nav>
  )
}
