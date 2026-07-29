import { FormFieldTypes, type FormFieldType, type FormFormulaNode } from './schemaTypes'
import { wouldCreateSelfReference } from './fieldDependencies'

export type FormulaField = { key: string; labelAr: string; type: FormFieldType }

const NUMERIC_TYPES = new Set<FormFieldType>([
  FormFieldTypes.Number,
  FormFieldTypes.Percentage,
  FormFieldTypes.CalculatedNumber,
])

const BINARY_LABELS_AR: Record<0 | 1 | 2 | 3 | 4, string> = {
  0: 'الجمع (+)',
  1: 'الطرح (-)',
  2: 'الضرب (×)',
  3: 'القسمة (÷)',
  4: 'باقي القسمة (%)',
}

const FUNCTION_LABELS_AR: Record<number, string> = {
  0: 'الحد الأدنى (Min)',
  1: 'الحد الأعلى (Max)',
  2: 'المجموع (Sum)',
  3: 'المتوسط (Average)',
  4: 'تقريب (Round)',
  5: 'جزء صحيح لأسفل (Floor)',
  6: 'جزء صحيح لأعلى (Ceiling)',
  7: 'القيمة المطلقة (Abs)',
  8: 'أول قيمة غير فارغة (Coalesce)',
  9: 'دمج نصي (Concat)',
}

// Functions that only make sense over numeric operands; Coalesce/Concat accept any field type.
const NUMERIC_ONLY_FUNCTIONS = new Set([0, 1, 2, 3, 4, 5, 6, 7])
const VARIADIC_FUNCTIONS = new Set([0, 1, 2, 3, 8, 9])

/** A stable key for a formula argument node. `FormFormulaNode` (mirroring the server's
 * polymorphic record) has no id, and arguments can be added/removed/reordered — so the key is
 * derived from the node's own content. `FormulaNodeEditor` keeps no per-instance local state,
 * so two arguments with identical content sharing a key is harmless. */
export function formulaNodeKey(node: FormFormulaNode): string {
  return JSON.stringify(node)
}

function defaultNodeForKind(kind: FormFormulaNode['kind'], numericField?: FormulaField): FormFormulaNode {
  switch (kind) {
    case 'constantNumber':
      return { kind: 'constantNumber', value: 0 }
    case 'constantText':
      return { kind: 'constantText', value: '' }
    case 'fieldReference':
      return { kind: 'fieldReference', fieldKey: numericField?.key ?? '' }
    case 'binary':
      return { kind: 'binary', operator: 0, left: { kind: 'constantNumber', value: 0 }, right: { kind: 'constantNumber', value: 0 } }
    case 'function':
      return { kind: 'function', function: 2, arguments: [{ kind: 'constantNumber', value: 0 }] }
  }
}

type FormulaNodeEditorProps = {
  node: FormFormulaNode | null
  onChange: (next: FormFormulaNode) => void
  availableFields: FormulaField[]
  excludeFieldKey?: string
  requireNumeric: boolean
}

function FormulaNodeEditor({ node, onChange, availableFields, excludeFieldKey, requireNumeric }: Readonly<FormulaNodeEditorProps>) {
  const selectableFields = (requireNumeric ? availableFields.filter((f) => NUMERIC_TYPES.has(f.type)) : availableFields)
    .filter((f) => !excludeFieldKey || !wouldCreateSelfReference(f.key, excludeFieldKey))

  const current = node ?? defaultNodeForKind('constantNumber')

  return (
    <span className="formula-node">
      <select
        aria-label="نوع العنصر"
        value={current.kind}
        onChange={(e) => onChange(defaultNodeForKind(e.target.value as FormFormulaNode['kind'], selectableFields[0]))}
      >
        <option value="constantNumber">رقم ثابت</option>
        <option value="constantText">نص ثابت</option>
        <option value="fieldReference">حقل</option>
        <option value="binary">عملية حسابية</option>
        <option value="function">دالة</option>
      </select>

      {current.kind === 'constantNumber' && (
        <input
          type="number"
          aria-label="القيمة الرقمية"
          value={current.value}
          onChange={(e) => onChange({ kind: 'constantNumber', value: Number(e.target.value) })}
        />
      )}

      {current.kind === 'constantText' && (
        <input
          type="text"
          aria-label="القيمة النصية"
          value={current.value}
          onChange={(e) => onChange({ kind: 'constantText', value: e.target.value })}
        />
      )}

      {current.kind === 'fieldReference' && (
        <select
          aria-label="الحقل المرجعي"
          value={current.fieldKey}
          onChange={(e) => onChange({ kind: 'fieldReference', fieldKey: e.target.value })}
        >
          <option value="">اختر حقلًا</option>
          {selectableFields.map((f) => (
            <option key={f.key} value={f.key}>{f.labelAr}</option>
          ))}
        </select>
      )}

      {current.kind === 'binary' && (
        <span className="formula-node-binary">
          <FormulaNodeEditor
            node={current.left}
            onChange={(next) => onChange({ ...current, left: next })}
            availableFields={availableFields}
            excludeFieldKey={excludeFieldKey}
            requireNumeric
          />
          <select
            aria-label="العملية"
            value={current.operator}
            onChange={(e) => onChange({ ...current, operator: Number(e.target.value) as typeof current.operator })}
          >
            {Object.entries(BINARY_LABELS_AR).map(([value, label]) => (
              <option key={value} value={value}>{label}</option>
            ))}
          </select>
          <FormulaNodeEditor
            node={current.right}
            onChange={(next) => onChange({ ...current, right: next })}
            availableFields={availableFields}
            excludeFieldKey={excludeFieldKey}
            requireNumeric
          />
          {current.operator === 3 && current.right.kind === 'constantNumber' && current.right.value === 0 && (
            <span className="field-error" role="alert">القسمة على صفر غير مسموحة.</span>
          )}
        </span>
      )}

      {current.kind === 'function' && (
        <span className="formula-node-function">
          <select
            aria-label="الدالة"
            value={current.function}
            onChange={(e) => {
              const fn = Number(e.target.value)
              onChange({
                kind: 'function',
                function: fn,
                arguments: current.arguments.length > 0 ? current.arguments : [defaultNodeForKind('constantNumber')],
              })
            }}
          >
            {Object.entries(FUNCTION_LABELS_AR).map(([value, label]) => (
              <option key={value} value={value}>{label}</option>
            ))}
          </select>
          <span className="formula-node-args">
            {current.arguments.map((arg, index) => (
              <span className="formula-node-arg" key={formulaNodeKey(arg)}>
                <FormulaNodeEditor
                  node={arg}
                  onChange={(next) => {
                    const args = current.arguments.map((a, i) => (i === index ? next : a))
                    onChange({ ...current, arguments: args })
                  }}
                  availableFields={availableFields}
                  excludeFieldKey={excludeFieldKey}
                  requireNumeric={NUMERIC_ONLY_FUNCTIONS.has(current.function)}
                />
                {VARIADIC_FUNCTIONS.has(current.function) && current.arguments.length > 1 && (
                  <button
                    type="button"
                    className="secondary"
                    aria-label="حذف المعامل"
                    onClick={() => onChange({ ...current, arguments: current.arguments.filter((_, i) => i !== index) })}
                  >
                    حذف
                  </button>
                )}
              </span>
            ))}
            {VARIADIC_FUNCTIONS.has(current.function) && (
              <button
                type="button"
                className="secondary"
                onClick={() => onChange({ ...current, arguments: [...current.arguments, defaultNodeForKind('constantNumber')] })}
              >
                + معامل
              </button>
            )}
          </span>
        </span>
      )}
    </span>
  )
}

type FormulaBuilderProps = {
  value: FormFormulaNode | null | undefined
  onChange: (next: FormFormulaNode | null) => void
  availableFields: FormulaField[]
  excludeFieldKey?: string
}

export function FormulaBuilder({ value, onChange, availableFields, excludeFieldKey }: Readonly<FormulaBuilderProps>) {
  if (!value) {
    return (
      <button type="button" className="secondary" onClick={() => onChange(defaultNodeForKind('binary'))}>
        + إضافة صيغة حساب
      </button>
    )
  }

  return (
    <fieldset className="formula-builder">
      <legend>مُنشئ الصيغة</legend>
      <FormulaNodeEditor
        node={value}
        onChange={onChange}
        availableFields={availableFields}
        excludeFieldKey={excludeFieldKey}
        requireNumeric={false}
      />
      <button type="button" className="secondary" onClick={() => onChange(null)}>إزالة الصيغة</button>
    </fieldset>
  )
}
