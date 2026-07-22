import { useMemo, useState } from 'react'
import { collectVisibleFields, evaluateCondition, evaluateFormula } from './previewLogic'
import { FormFieldTypeLabelsAr, type FormSchemaDocument } from './schemaTypes'

export function FormPreviewPanel({
  schema,
  mode,
  onModeChange,
  onClose,
}: Readonly<{
  schema: FormSchemaDocument
  mode: 'desktop' | 'tablet' | 'mobile'
  onModeChange: (mode: 'desktop' | 'tablet' | 'mobile') => void
  onClose: () => void
}>) {
  const [values, setValues] = useState<Record<string, unknown>>({})
  const [pageIndex, setPageIndex] = useState(0)
  const width = mode === 'mobile' ? 360 : mode === 'tablet' ? 768 : 1024
  const page = schema.pages[pageIndex]

  const visibleFields = useMemo(() => collectVisibleFields(schema, values), [schema, values])

  return (
    <div className="preview-shell" dir="rtl">
      <div className="toolbar">
        <button type="button" className={mode === 'desktop' ? undefined : 'secondary'} onClick={() => onModeChange('desktop')}>سطح المكتب</button>
        <button type="button" className={mode === 'tablet' ? undefined : 'secondary'} onClick={() => onModeChange('tablet')}>لوحي</button>
        <button type="button" className={mode === 'mobile' ? undefined : 'secondary'} onClick={() => onModeChange('mobile')}>جوال</button>
        <button type="button" className="secondary" onClick={onClose}>إغلاق المعاينة</button>
      </div>
      <p className="muted">المعاينة لا تحفظ ردودًا تشغيلية ولا ترسل بيانات.</p>
      <div className="preview-frame" style={{ maxWidth: width, marginInline: 'auto' }}>
        <div className="toolbar">
          {schema.pages.map((p, i) => (
            <button key={p.id} type="button" className={i === pageIndex ? undefined : 'secondary'} onClick={() => setPageIndex(i)}>
              {p.titleAr}
            </button>
          ))}
        </div>
        {page && evaluateCondition(page.visibilityCondition, values) ? (
          page.sections
            .filter((s) => evaluateCondition(s.visibilityCondition, values))
            .map((section) => (
              <section key={section.id} className="panel-section">
                <h3 className="section-title">{section.titleAr}</h3>
                {section.fields
                  .filter((f) => evaluateCondition(f.visibilityCondition, values))
                  .map((field) => {
                    const required =
                      field.isRequired ||
                      (field.requiredCondition ? evaluateCondition(field.requiredCondition, values) : false)
                    const calculated = field.isCalculated
                      ? evaluateFormula(field.formula, values)
                      : null
                    return (
                      <label key={field.id} className="field">
                        {field.labelAr}
                        {required ? ' *' : ''}
                        <span className="muted"> — {FormFieldTypeLabelsAr[field.type]}</span>
                        {field.isCalculated ? (
                          <input readOnly value={calculated == null ? '' : String(calculated)} />
                        ) : (
                          <input
                            value={String(values[field.key] ?? field.defaultValue ?? '')}
                            onChange={(e) => setValues((v) => ({ ...v, [field.key]: e.target.value }))}
                            placeholder={field.placeholder ?? undefined}
                          />
                        )}
                      </label>
                    )
                  })}
              </section>
            ))
        ) : (
          <div className="empty">الصفحة غير ظاهرة وفق الشروط.</div>
        )}
        <div className="muted">الحقول الظاهرة: {visibleFields.length}</div>
      </div>
    </div>
  )
}
