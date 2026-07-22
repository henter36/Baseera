import { useMemo, useState } from 'react'
import { getPreviewWidth } from './formPreviewWidths'
import { collectVisibleFields, evaluateCondition, evaluateFormula, toDisplayString } from './previewLogic'
import { FormFieldTypes, FormFieldTypeLabelsAr, type FormFieldSchema, type FormPageSchema, type FormSchemaDocument, type FormSectionSchema } from './schemaTypes'

function resolvePreviewInputType(fieldType: number): string {
  switch (fieldType) {
    case FormFieldTypes.Number:
    case FormFieldTypes.Percentage:
    case FormFieldTypes.CalculatedNumber:
      return 'number'
    case FormFieldTypes.Date:
      return 'date'
    case FormFieldTypes.Time:
      return 'time'
    case FormFieldTypes.DateTime:
      return 'datetime-local'
    default:
      return 'text'
  }
}

function PreviewField({
  field,
  values,
  onValueChange,
}: Readonly<{
  field: FormFieldSchema
  values: Record<string, unknown>
  onValueChange: (key: string, value: string) => void
}>) {
  const required =
    field.isRequired ||
    (field.requiredCondition ? evaluateCondition(field.requiredCondition, values) : false)
  const calculatedValue = field.isCalculated ? evaluateFormula(field.formula, values) : null
  const inputType = resolvePreviewInputType(field.type)

  return (
    <label className="field">
      {field.labelAr}
      {required ? ' *' : ''}
      <span className="muted"> — {FormFieldTypeLabelsAr[field.type]}</span>
      {field.isCalculated ? (
        <input readOnly value={toDisplayString(calculatedValue)} />
      ) : (
        <input
          type={inputType}
          value={toDisplayString(values[field.key] ?? field.defaultValue ?? '')}
          onChange={(event) => onValueChange(field.key, event.target.value)}
          placeholder={field.placeholder ?? undefined}
        />
      )}
    </label>
  )
}

function PreviewSection({
  section,
  values,
  onValueChange,
}: Readonly<{
  section: FormSectionSchema
  values: Record<string, unknown>
  onValueChange: (key: string, value: string) => void
}>) {
  if (!evaluateCondition(section.visibilityCondition, values)) {
    return null
  }

  const visibleFields = section.fields.filter((field) => evaluateCondition(field.visibilityCondition, values))
  if (visibleFields.length === 0) {
    return null
  }

  return (
    <section className="panel-section">
      <h3 className="section-title">{section.titleAr}</h3>
      {visibleFields.map((field) => (
        <PreviewField key={field.id} field={field} values={values} onValueChange={onValueChange} />
      ))}
    </section>
  )
}

function PreviewPage({
  page,
  values,
  onValueChange,
}: Readonly<{
  page: FormPageSchema
  values: Record<string, unknown>
  onValueChange: (key: string, value: string) => void
}>) {
  if (!evaluateCondition(page.visibilityCondition, values)) {
    return <div className="empty">الصفحة غير ظاهرة وفق الشروط.</div>
  }

  return (
    <>
      {page.sections.map((section) => (
        <PreviewSection key={section.id} section={section} values={values} onValueChange={onValueChange} />
      ))}
    </>
  )
}

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
  const width = getPreviewWidth(mode)
  const page = schema.pages[pageIndex]

  const visibleFields = useMemo(() => collectVisibleFields(schema, values), [schema, values])

  const handleValueChange = (key: string, value: string) => {
    setValues((current) => ({ ...current, [key]: value }))
  }

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
          {schema.pages.map((schemaPage, index) => (
            <button key={schemaPage.id} type="button" className={index === pageIndex ? undefined : 'secondary'} onClick={() => setPageIndex(index)}>
              {schemaPage.titleAr}
            </button>
          ))}
        </div>
        {page ? <PreviewPage page={page} values={values} onValueChange={handleValueChange} /> : null}
        <div className="muted">الحقول الظاهرة: {visibleFields.length}</div>
      </div>
    </div>
  )
}
