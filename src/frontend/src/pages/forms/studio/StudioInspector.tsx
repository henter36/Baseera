import { useEffect, useState } from 'react'
import { ConditionBuilder } from '../../../forms/designer/ConditionBuilder'
import { FormulaBuilder } from '../../../forms/designer/FormulaBuilder'
import { renameFieldKeyInSchema, updateFieldInSchema } from '../../../forms/designer/designerHelpers'
import { detectDependencyCycle, flattenFields } from '../../../forms/designer/fieldDependencies'
import {
  FormFieldTypes,
  reindexChoice,
  type FormFieldOption,
  type FormFieldSchema,
  type FormPageSchema,
  type FormSchemaDocument,
} from '../../../forms/designer/schemaTypes'

const CHOICE_TYPES = new Set<number>([FormFieldTypes.SingleChoice, FormFieldTypes.MultipleChoice])
const TEXT_TYPES = new Set<number>([FormFieldTypes.ShortText, FormFieldTypes.LongText])
const NUMBER_TYPES = new Set<number>([FormFieldTypes.Number, FormFieldTypes.Percentage])
const FILE_TYPES = new Set<number>([FormFieldTypes.File, FormFieldTypes.Image, FormFieldTypes.Signature])
const CALCULATED_TYPES = new Set<number>([FormFieldTypes.CalculatedNumber, FormFieldTypes.CalculatedText])

function commitOnEnter(event: React.KeyboardEvent<HTMLInputElement>, commit: () => void) {
  if (event.key === 'Enter') {
    event.currentTarget.blur()
    commit()
  }
}

type ValidationIssue = { code: string; path: string; messageAr: string; severity: number }

type StudioInspectorProps = {
  schema: FormSchemaDocument
  page: FormPageSchema | undefined
  selectedField: FormFieldSchema | undefined
  issues: ValidationIssue[]
  onApplySchema: (next: FormSchemaDocument) => void
}

export function StudioInspector({ schema, page, selectedField, issues, onApplySchema }: Readonly<StudioInspectorProps>) {
  const [draftLabelAr, setDraftLabelAr] = useState('')
  const [draftKey, setDraftKey] = useState('')
  const [draftDescription, setDraftDescription] = useState('')
  const [draftDefaultValue, setDraftDefaultValue] = useState('')
  const [keyError, setKeyError] = useState<string | null>(null)
  const [cycleError, setCycleError] = useState<string | null>(null)

  useEffect(() => {
    if (!selectedField) return
    setDraftLabelAr(selectedField.labelAr)
    setDraftKey(selectedField.key)
    setDraftDescription(selectedField.description ?? '')
    setDraftDefaultValue(selectedField.defaultValue ?? '')
    setKeyError(null)
    setCycleError(null)
  }, [selectedField])

  if (!selectedField || !page) {
    return (
      <aside className="studio-inspector" aria-label="خصائص العنصر">
        <h2 className="section-title">Inspector</h2>
        <div className="empty">اختر حقلًا من Canvas أو المخطط لتعديل خصائصه.</div>
      </aside>
    )
  }

  const field = selectedField
  const availableFields = flattenFields(schema).map(({ field: f }) => ({
    key: f.key,
    labelAr: f.labelAr,
    type: f.type,
    choiceOptions: f.choice?.options,
  }))

  const applyPatch = (patch: Partial<FormFieldSchema>) => {
    onApplySchema(updateFieldInSchema(schema, page.id, field.id, patch))
  }

  /** Simulates the patch before applying it so a condition/formula edit that would introduce a
   * circular dependency is caught in the UI, mirroring (not replacing) the server's own check. */
  const applyPatchWithCycleGuard = (patch: Partial<FormFieldSchema>) => {
    const candidate = updateFieldInSchema(schema, page.id, field.id, patch)
    const cycle = detectDependencyCycle(candidate)
    if (cycle) {
      setCycleError(`هذا التغيير يُنشئ دورة اعتماد بين الحقول: ${cycle.join(' ← ')}`)
      return
    }
    setCycleError(null)
    onApplySchema(candidate)
  }

  const commitLabelAr = () => {
    if (draftLabelAr !== field.labelAr) applyPatch({ labelAr: draftLabelAr })
  }

  const commitKey = () => {
    if (draftKey === field.key) { setKeyError(null); return }
    const result = renameFieldKeyInSchema(schema, field.id, draftKey)
    if (!result.ok) {
      setKeyError(result.error)
      setDraftKey(field.key)
      return
    }
    setKeyError(null)
    onApplySchema(result.schema)
  }

  const commitDescription = () => {
    const normalized = draftDescription.length > 0 ? draftDescription : null
    if (normalized !== (field.description ?? null)) applyPatch({ description: normalized })
  }

  const commitDefaultValue = () => {
    const normalized = draftDefaultValue.length > 0 ? draftDefaultValue : null
    if (normalized !== (field.defaultValue ?? null)) applyPatch({ defaultValue: normalized })
  }

  const fieldIssues = issues.filter((i) => i.path?.toLowerCase().includes(field.key.toLowerCase()))
  const idPrefix = `studio-inspector-${field.id}`

  return (
    <aside className="studio-inspector" aria-label="خصائص العنصر">
      <h2 className="section-title">خصائص الحقل</h2>

      <div className="form-grid">
        <label className="field">
          <span>عنوان الحقل</span>
          <input id={`${idPrefix}-label`} value={draftLabelAr} onChange={(e) => setDraftLabelAr(e.target.value)} onBlur={commitLabelAr} onKeyDown={(e) => commitOnEnter(e, commitLabelAr)} />
        </label>

        <label className="field">
          <span>المفتاح</span>
          <input id={`${idPrefix}-key`} value={draftKey} onChange={(e) => { setDraftKey(e.target.value); setKeyError(null) }} onBlur={commitKey} onKeyDown={(e) => commitOnEnter(e, commitKey)} aria-invalid={keyError ? true : undefined} />
          {keyError && <span className="field-error">{keyError}</span>}
        </label>

        <label className="field field-wide">
          <span>الوصف أو النص الإرشادي</span>
          <textarea id={`${idPrefix}-description`} rows={2} value={draftDescription} onChange={(e) => setDraftDescription(e.target.value)} onBlur={commitDescription} />
        </label>

        {!field.isCalculated && (
          <label className="checkbox-field">
            <input type="checkbox" checked={field.isRequired} onChange={(e) => applyPatch({ isRequired: e.target.checked })} />
            <span>إلزامي</span>
          </label>
        )}

        {!field.isCalculated && !CHOICE_TYPES.has(field.type) && (
          <label className="field">
            <span>القيمة الافتراضية</span>
            <input value={draftDefaultValue} onChange={(e) => setDraftDefaultValue(e.target.value)} onBlur={commitDefaultValue} onKeyDown={(e) => commitOnEnter(e, commitDefaultValue)} />
          </label>
        )}

        {TEXT_TYPES.has(field.type) && (
          <>
            <label className="field">
              <span>الحد الأدنى لعدد الأحرف</span>
              <input type="number" min={0} value={field.text?.minLength ?? ''} onChange={(e) => applyPatch({ text: { ...(field.text ?? { kind: 0 }), minLength: e.target.value === '' ? null : Number(e.target.value) } })} />
            </label>
            <label className="field">
              <span>الحد الأعلى لعدد الأحرف</span>
              <input type="number" min={0} value={field.text?.maxLength ?? ''} onChange={(e) => applyPatch({ text: { ...(field.text ?? { kind: 0 }), maxLength: e.target.value === '' ? null : Number(e.target.value) } })} />
            </label>
          </>
        )}

        {NUMBER_TYPES.has(field.type) && (
          <>
            <label className="field">
              <span>الحد الأدنى</span>
              <input type="number" value={field.number?.min ?? ''} onChange={(e) => applyPatch({ number: { ...field.number, min: e.target.value === '' ? null : Number(e.target.value) } })} />
            </label>
            <label className="field">
              <span>الحد الأعلى</span>
              <input type="number" value={field.number?.max ?? ''} onChange={(e) => applyPatch({ number: { ...field.number, max: e.target.value === '' ? null : Number(e.target.value) } })} />
            </label>
            <label className="field">
              <span>صيغة العرض (عدد المنازل العشرية)</span>
              <input type="number" min={0} max={6} value={field.number?.decimalPlaces ?? ''} onChange={(e) => applyPatch({ number: { ...field.number, decimalPlaces: e.target.value === '' ? null : Number(e.target.value) } })} />
            </label>
          </>
        )}

        {CHOICE_TYPES.has(field.type) && (
          <ChoiceOptionsEditor field={field} onChange={(choice) => applyPatch({ choice })} />
        )}

        {FILE_TYPES.has(field.type) && (
          <>
            <label className="checkbox-field">
              <input type="checkbox" checked={(field.file?.maxFiles ?? 1) > 0} onChange={(e) => applyPatch({ file: { ...(field.file ?? defaultFileSettings()), maxFiles: e.target.checked ? Math.max(1, field.file?.maxFiles ?? 1) : 0 } })} />
              <span>السماح بالمرفقات</span>
            </label>
            <label className="field">
              <span>عدد الملفات</span>
              <input type="number" min={0} value={field.file?.maxFiles ?? 1} onChange={(e) => applyPatch({ file: { ...(field.file ?? defaultFileSettings()), maxFiles: Number(e.target.value) } })} />
            </label>
            <label className="field field-wide">
              <span>أنواع الملفات المسموحة (امتدادات مفصولة بفواصل)</span>
              <input value={(field.file?.allowedExtensions ?? []).join(', ')} onChange={(e) => applyPatch({ file: { ...(field.file ?? defaultFileSettings()), allowedExtensions: e.target.value.split(',').map((s) => s.trim()).filter(Boolean) } })} />
            </label>
          </>
        )}

        {field.type === FormFieldTypes.OrganizationalReference && (
          <label className="field">
            <span>نوع المرجع التنظيمي</span>
            <select value={field.organizationalReference?.kind ?? 0} onChange={(e) => applyPatch({ organizationalReference: { kind: Number(e.target.value) } })}>
              <option value={0}>المنطقة</option>
              <option value={1}>السجن</option>
              <option value={2}>الوحدة</option>
              <option value={3}>الإدارة</option>
            </select>
          </label>
        )}
      </div>

      {fieldIssues.length > 0 && (
        <div className="panel-section">
          <h3 className="section-title">تحقق هذا الحقل</h3>
          <ul>
            {fieldIssues.map((issue) => (
              <li key={`${issue.code}-${issue.path}`} className={issue.severity === 0 ? 'error' : 'warn'}>{issue.messageAr}</li>
            ))}
          </ul>
        </div>
      )}

      <details className="studio-advanced">
        <summary>إعدادات متقدمة</summary>

        {cycleError && <div className="error" role="alert">{cycleError}</div>}

        <div className="panel-section">
          <h3 className="section-title">شرط الظهور</h3>
          <ConditionBuilder
            value={field.visibilityCondition}
            onChange={(next) => applyPatchWithCycleGuard({ visibilityCondition: next })}
            availableFields={availableFields}
            excludeFieldKey={field.key}
          />
        </div>

        <div className="panel-section">
          <h3 className="section-title">شرط الإلزام</h3>
          <ConditionBuilder
            value={field.requiredCondition}
            onChange={(next) => applyPatchWithCycleGuard({ requiredCondition: next })}
            availableFields={availableFields}
            excludeFieldKey={field.key}
          />
        </div>

        {CALCULATED_TYPES.has(field.type) && (
          <div className="panel-section">
            <h3 className="section-title">صيغة الحساب</h3>
            <FormulaBuilder
              value={field.formula}
              onChange={(next) => applyPatchWithCycleGuard({ formula: next })}
              availableFields={availableFields}
              excludeFieldKey={field.key}
            />
          </div>
        )}

        <div className="panel-section">
          <h3 className="section-title">مفاتيح التكامل وربط البيانات</h3>
          <p className="muted">مفتاح الحقل («{field.key}») هو معرف التكامل الوحيد المتاح حاليًا؛ لا توجد إعدادات ربط بيانات خارجية إضافية في هذا الإصدار.</p>
        </div>
      </details>
    </aside>
  )
}

function defaultFileSettings(): NonNullable<FormFieldSchema['file']> {
  return { maxFiles: 1, maxFileSizeBytes: 5_000_000, allowedMimeTypes: [], allowedExtensions: [], requireVirusScan: true }
}

function ChoiceOptionsEditor({ field, onChange }: Readonly<{ field: FormFieldSchema; onChange: (choice: NonNullable<FormFieldSchema['choice']>) => void }>) {
  const choice = field.choice ?? { options: [], allowOther: false }
  const [keyErrors, setKeyErrors] = useState<Record<number, string>>({})

  const setOptions = (options: FormFieldOption[]) => onChange(reindexChoice({ ...choice, options }))

  const updateOption = (index: number, patch: Partial<FormFieldOption>) => {
    if (patch.value !== undefined) {
      const normalized = patch.value.trim()
      const duplicate = choice.options.some((o, i) => i !== index && o.value === normalized)
      setKeyErrors((prev) => ({ ...prev, [index]: duplicate ? 'مفتاح مكرر.' : '' }))
      if (duplicate) {
        return
      }
    }
    setOptions(choice.options.map((o, i) => (i === index ? { ...o, ...patch } : o)))
  }

  const addOption = () => {
    const nextIndex = choice.options.length + 1
    setOptions([...choice.options, { value: `option_${nextIndex}`, labelAr: `خيار ${nextIndex}`, order: choice.options.length, isActive: true }])
  }

  const removeOption = (index: number) => setOptions(choice.options.filter((_, i) => i !== index))

  const moveOption = (index: number, direction: -1 | 1) => {
    const target = index + direction
    if (target < 0 || target >= choice.options.length) return
    const next = [...choice.options]
    ;[next[index], next[target]] = [next[target], next[index]]
    setOptions(next)
  }

  return (
    <div className="field field-wide">
      <span>الخيارات</span>
      {choice.options.map((option, index) => (
        <div key={option.value} className="designer-row">
          <input aria-label={`نص الخيار ${index + 1}`} value={option.labelAr} onChange={(e) => updateOption(index, { labelAr: e.target.value })} />
          <input aria-label={`مفتاح الخيار ${index + 1}`} value={option.value} onChange={(e) => updateOption(index, { value: e.target.value })} />
          {keyErrors[index] && <span className="field-error">{keyErrors[index]}</span>}
          <button type="button" className="secondary" aria-label="نقل لأعلى" onClick={() => moveOption(index, -1)}>↑</button>
          <button type="button" className="secondary" aria-label="نقل لأسفل" onClick={() => moveOption(index, 1)}>↓</button>
          <button type="button" className="secondary" aria-label="حذف الخيار" onClick={() => removeOption(index)}>حذف</button>
        </div>
      ))}
      <button type="button" className="secondary" onClick={addOption}>+ خيار</button>
      <label className="checkbox-field">
        <input type="checkbox" checked={choice.allowOther} onChange={(e) => onChange({ ...choice, allowOther: e.target.checked })} />
        <span>السماح بخيار "أخرى"</span>
      </label>
    </div>
  )
}
