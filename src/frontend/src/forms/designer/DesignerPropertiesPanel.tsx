import { updateFieldInSchema } from './designerHelpers'
import type { FormFieldSchema, FormPageSchema, FormSchemaDocument } from './schemaTypes'

type ValidationIssue = {
  code: string
  path: string
  messageAr: string
  severity: number
}

type DesignerPropertiesPanelProps = {
  schema: FormSchemaDocument
  page: FormPageSchema | undefined
  selectedField: FormFieldSchema | undefined
  issues: ValidationIssue[]
  onApplySchema: (next: FormSchemaDocument) => void
}

export function DesignerPropertiesPanel({
  schema,
  page,
  selectedField,
  issues,
  onApplySchema,
}: Readonly<DesignerPropertiesPanelProps>) {
  return (
    <aside className="designer-props" aria-label="خصائص الحقل">
      <h2 className="section-title">الخصائص</h2>
      {!selectedField || !page ? (
        <div className="empty">اختر حقلًا لتعديل خصائصه.</div>
      ) : (
        <div className="form-grid">
          <label className="field">
            التسمية العربية
            <input
              value={selectedField.labelAr}
              onChange={(e) => onApplySchema(updateFieldInSchema(schema, page.id, selectedField.id, { labelAr: e.target.value }))}
            />
          </label>
          <label className="field">
            المفتاح
            <input
              value={selectedField.key}
              onChange={(e) => onApplySchema(updateFieldInSchema(schema, page.id, selectedField.id, { key: e.target.value }))}
            />
          </label>
          <label className="field">
            <input
              type="checkbox"
              checked={selectedField.isRequired}
              onChange={(e) => onApplySchema(updateFieldInSchema(schema, page.id, selectedField.id, { isRequired: e.target.checked }))}
            />{' '}
            إلزامي
          </label>
          <label className="field">
            القيمة الافتراضية
            <input
              value={selectedField.defaultValue ?? ''}
              onChange={(e) => onApplySchema(updateFieldInSchema(schema, page.id, selectedField.id, { defaultValue: e.target.value }))}
            />
          </label>
        </div>
      )}
      <h2 className="section-title">التحقق</h2>
      {issues.length === 0 ? (
        <div className="muted">لا توجد مشاكل معروضة.</div>
      ) : (
        <ul>
          {issues.map((issue, i) => (
            <li key={`${issue.code}-${i}`} className={issue.severity === 0 ? 'error' : 'muted'}>
              {issue.messageAr} <span className="muted">({issue.path})</span>
            </li>
          ))}
        </ul>
      )}
    </aside>
  )
}
