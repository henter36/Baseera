import { useEffect, useState } from 'react'
import { renameFieldKeyInSchema, updateFieldInSchema } from './designerHelpers'
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

function commitOnEnter(event: React.KeyboardEvent<HTMLInputElement>, commit: () => void) {
  if (event.key === 'Enter') {
    event.currentTarget.blur()
    commit()
  }
}

export function DesignerPropertiesPanel({
  schema,
  page,
  selectedField,
  issues,
  onApplySchema,
}: Readonly<DesignerPropertiesPanelProps>) {
  const [draftLabelAr, setDraftLabelAr] = useState('')
  const [draftKey, setDraftKey] = useState('')
  const [draftDefaultValue, setDraftDefaultValue] = useState('')
  const [keyError, setKeyError] = useState<string | null>(null)

  useEffect(() => {
    if (!selectedField) {
      return
    }

    setDraftLabelAr(selectedField.labelAr)
    setDraftKey(selectedField.key)
    setDraftDefaultValue(selectedField.defaultValue ?? '')
    setKeyError(null)
  }, [selectedField])

  const commitLabelAr = () => {
    if (!selectedField || !page || draftLabelAr === selectedField.labelAr) {
      return
    }

    onApplySchema(updateFieldInSchema(schema, page.id, selectedField.id, { labelAr: draftLabelAr }))
  }

  const commitKey = () => {
    if (!selectedField || !page) {
      return
    }

    if (draftKey === selectedField.key) {
      setKeyError(null)
      return
    }

    const result = renameFieldKeyInSchema(schema, selectedField.id, draftKey)
    if (!result.ok) {
      setKeyError(result.error)
      setDraftKey(selectedField.key)
      return
    }

    setKeyError(null)
    onApplySchema(result.schema)
  }

  const commitDefaultValue = () => {
    if (!selectedField || !page) {
      return
    }

    const normalized = draftDefaultValue.length > 0 ? draftDefaultValue : null
    if (normalized === (selectedField.defaultValue ?? null)) {
      return
    }

    onApplySchema(updateFieldInSchema(schema, page.id, selectedField.id, { defaultValue: normalized }))
  }

  const fieldIdPrefix = selectedField ? `field-props-${selectedField.id}` : 'field-props'

  return (
    <aside className="designer-props" aria-label="خصائص الحقل">
      <h2 className="section-title">الخصائص</h2>
      {!selectedField || !page ? (
        <div className="empty">اختر حقلًا لتعديل خصائصه.</div>
      ) : (
        <div className="form-grid">
          <div className="field">
            <label htmlFor={`${fieldIdPrefix}-label-ar`}>
              <span>التسمية العربية</span>
            </label>
            <input
              id={`${fieldIdPrefix}-label-ar`}
              value={draftLabelAr}
              onChange={(event) => setDraftLabelAr(event.target.value)}
              onBlur={commitLabelAr}
              onKeyDown={(event) => commitOnEnter(event, commitLabelAr)}
            />
          </div>
          <div className="field">
            <label htmlFor={`${fieldIdPrefix}-key`}>
              <span>المفتاح</span>
            </label>
            <input
              id={`${fieldIdPrefix}-key`}
              value={draftKey}
              onChange={(event) => {
                setDraftKey(event.target.value)
                setKeyError(null)
              }}
              onBlur={commitKey}
              onKeyDown={(event) => commitOnEnter(event, commitKey)}
              aria-invalid={keyError ? true : undefined}
            />
            {keyError ? <span className="field-error">{keyError}</span> : null}
          </div>
          <label className="checkbox-field" htmlFor={`${fieldIdPrefix}-required`}>
            <input
              id={`${fieldIdPrefix}-required`}
              type="checkbox"
              checked={selectedField.isRequired}
              onChange={(event) =>
                onApplySchema(updateFieldInSchema(schema, page.id, selectedField.id, { isRequired: event.target.checked }))
              }
            />
            <span>إلزامي</span>
          </label>
          <div className="field">
            <label htmlFor={`${fieldIdPrefix}-default`}>
              <span>القيمة الافتراضية</span>
            </label>
            <input
              id={`${fieldIdPrefix}-default`}
              value={draftDefaultValue}
              onChange={(event) => setDraftDefaultValue(event.target.value)}
              onBlur={commitDefaultValue}
              onKeyDown={(event) => commitOnEnter(event, commitDefaultValue)}
            />
          </div>
        </div>
      )}
      <h2 className="section-title">التحقق</h2>
      {issues.length === 0 ? (
        <div className="muted">لا توجد مشاكل معروضة.</div>
      ) : (
        <ul>
          {issues.map((issue, index) => (
            <li key={`${issue.code}-${index}`} className={issue.severity === 0 ? 'error' : 'muted'}>
              {issue.messageAr} <span className="muted">({issue.path})</span>
            </li>
          ))}
        </ul>
      )}
    </aside>
  )
}
