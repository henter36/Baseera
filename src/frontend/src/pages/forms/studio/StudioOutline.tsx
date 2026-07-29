import { useState } from 'react'
import type { FormFieldSchema, FormPageSchema, FormSchemaDocument, FormSectionSchema } from '../../../forms/designer/schemaTypes'

function OutlineField({
  field,
  isSelected,
  onSelect,
}: Readonly<{ field: FormFieldSchema; isSelected: boolean; onSelect: () => void }>) {
  return (
    <button
      type="button"
      className={isSelected ? 'studio-outline-field' : 'studio-outline-field secondary'}
      onClick={onSelect}
    >
      {field.labelAr}
    </button>
  )
}

function OutlineSection({
  section,
  selectedFieldId,
  onSelectField,
}: Readonly<{ section: FormSectionSchema; selectedFieldId: string | null; onSelectField: (fieldId: string) => void }>) {
  return (
    <div className="studio-outline-section">
      <div className="muted">{section.titleAr}</div>
      {section.fields.length === 0 ? (
        <div className="studio-outline-field muted">لا حقول</div>
      ) : (
        section.fields.map((field) => (
          <OutlineField key={field.id} field={field} isSelected={field.id === selectedFieldId} onSelect={() => onSelectField(field.id)} />
        ))
      )}
    </div>
  )
}

function OutlinePage({
  page,
  selectedFieldId,
  errorCount,
  collapsed,
  onToggle,
  onSelectField,
}: Readonly<{
  page: FormPageSchema
  selectedFieldId: string | null
  errorCount: number
  collapsed: boolean
  onToggle: () => void
  onSelectField: (fieldId: string) => void
}>) {
  const fieldCount = page.sections.reduce((n, s) => n + s.fields.length, 0)
  return (
    <div className="studio-outline-page">
      <button type="button" className="secondary" aria-expanded={!collapsed} onClick={onToggle}>
        {collapsed ? '▸' : '▾'} {page.titleAr}
        {' '}<span className="studio-outline-count">({fieldCount} حقل)</span>
        {errorCount > 0 && <span className="studio-outline-error-count"> — {errorCount} خطأ</span>}
      </button>
      {!collapsed && page.sections.map((section) => (
        <OutlineSection key={section.id} section={section} selectedFieldId={selectedFieldId} onSelectField={onSelectField} />
      ))}
    </div>
  )
}

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
      {schema.pages.map((page) => (
        <OutlinePage
          key={page.id}
          page={page}
          selectedFieldId={selectedFieldId}
          errorCount={errorCountByPageId(page.id)}
          collapsed={collapsedPages.has(page.id)}
          onToggle={() => togglePage(page.id)}
          onSelectField={(fieldId) => onSelectField(page.id, fieldId)}
        />
      ))}
    </nav>
  )
}
