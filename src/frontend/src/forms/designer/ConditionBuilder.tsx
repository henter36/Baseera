import { FormFieldTypes, type FormConditionGroup, type FormConditionOperator, type FormFieldOption, type FormFieldType } from './schemaTypes'
import { wouldCreateSelfReference } from './fieldDependencies'

export type ConditionableField = {
  key: string
  labelAr: string
  type: FormFieldType
  choiceOptions?: FormFieldOption[]
}

const OPERATOR_LABELS_AR: Record<FormConditionOperator, string> = {
  0: 'يساوي',
  1: 'لا يساوي',
  2: 'أكبر من',
  3: 'أكبر من أو يساوي',
  4: 'أقل من',
  5: 'أقل من أو يساوي',
  6: 'يحتوي',
  7: 'لا يحتوي',
  8: 'فارغ',
  9: 'غير فارغ',
  10: 'صحيح (نعم)',
  11: 'خاطئ (لا)',
  12: 'أحد الخيارات',
  13: 'ليس أحد الخيارات',
  14: 'قبل',
  15: 'بعد',
}

const TEXT_OPERATORS: FormConditionOperator[] = [0, 1, 6, 7, 8, 9]
const NUMBER_OPERATORS: FormConditionOperator[] = [0, 1, 2, 3, 4, 5, 8, 9]
const DATE_OPERATORS: FormConditionOperator[] = [0, 1, 14, 15, 8, 9]
const CHOICE_OPERATORS: FormConditionOperator[] = [0, 1, 12, 13, 8, 9]
const BOOLEAN_OPERATORS: FormConditionOperator[] = [10, 11, 8, 9]
const STRUCTURAL_OPERATORS: FormConditionOperator[] = [8, 9]

const NO_VALUE_OPERATORS = new Set<FormConditionOperator>([8, 9, 10, 11])
const MULTI_VALUE_OPERATORS = new Set<FormConditionOperator>([12, 13])

// Operator sets are restricted per field type so the builder never offers an incompatible
// comparison (e.g. "greater than" on a text field). This mirrors, but does not replace, the
// server-side type checks in FormSchemaValidator — validateVersion remains authoritative.
function operatorsForType(type: FormFieldType): FormConditionOperator[] {
  switch (type) {
    case FormFieldTypes.Number:
    case FormFieldTypes.Percentage:
    case FormFieldTypes.CalculatedNumber:
      return NUMBER_OPERATORS
    case FormFieldTypes.Date:
    case FormFieldTypes.Time:
    case FormFieldTypes.DateTime:
      return DATE_OPERATORS
    case FormFieldTypes.SingleChoice:
    case FormFieldTypes.MultipleChoice:
      return CHOICE_OPERATORS
    case FormFieldTypes.YesNo:
      return BOOLEAN_OPERATORS
    case FormFieldTypes.ShortText:
    case FormFieldTypes.LongText:
    case FormFieldTypes.CalculatedText:
      return TEXT_OPERATORS
    default:
      return STRUCTURAL_OPERATORS
  }
}

function inputTypeForField(type: FormFieldType): string {
  switch (type) {
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

function emptyGroup(): FormConditionGroup {
  return { combinator: 0, predicates: [], groups: [] }
}

type ConditionBuilderProps = {
  value: FormConditionGroup | null | undefined
  onChange: (next: FormConditionGroup | null) => void
  availableFields: ConditionableField[]
  excludeFieldKey?: string
  depth?: number
}

export function ConditionBuilder({
  value,
  onChange,
  availableFields,
  excludeFieldKey,
  depth = 0,
}: Readonly<ConditionBuilderProps>) {
  const group = value ?? null
  const selectableFields = excludeFieldKey
    ? availableFields.filter((f) => !wouldCreateSelfReference(f.key, excludeFieldKey))
    : availableFields

  if (!group) {
    return (
      <button type="button" className="secondary" onClick={() => onChange(emptyGroup())}>
        + إضافة شرط
      </button>
    )
  }

  const updatePredicate = (index: number, patch: Partial<FormConditionGroup['predicates'][number]>) => {
    const predicates = group.predicates.map((p, i) => (i === index ? { ...p, ...patch } : p))
    onChange({ ...group, predicates })
  }

  const removePredicate = (index: number) => {
    onChange({ ...group, predicates: group.predicates.filter((_, i) => i !== index) })
  }

  const addPredicate = () => {
    const firstField = selectableFields[0]
    if (!firstField) return
    const operators = operatorsForType(firstField.type)
    onChange({
      ...group,
      predicates: [...group.predicates, { fieldKey: firstField.key, operator: operators[0], value: '' }],
    })
  }

  const addNestedGroup = () => {
    onChange({ ...group, groups: [...group.groups, emptyGroup()] })
  }

  const updateNestedGroup = (index: number, next: FormConditionGroup | null) => {
    const groups = group.groups.map((g, i) => (i === index ? next ?? emptyGroup() : g))
    onChange({ ...group, groups })
  }

  const removeNestedGroup = (index: number) => {
    onChange({ ...group, groups: group.groups.filter((_, i) => i !== index) })
  }

  return (
    <fieldset className="condition-builder">
      <legend>مُنشئ الشرط</legend>
      <div className="condition-builder-row">
        <span>عندما تتحقق</span>
        <select
          aria-label="مطابقة المجموعة"
          value={group.combinator}
          onChange={(e) => onChange({ ...group, combinator: Number(e.target.value) as 0 | 1 })}
        >
          <option value={0}>كل الشروط</option>
          <option value={1}>أي شرط</option>
        </select>
      </div>

      {group.predicates.map((predicate, index) => {
        const field = selectableFields.find((f) => f.key.toLowerCase() === predicate.fieldKey.toLowerCase())
        const operators = field ? operatorsForType(field.type) : TEXT_OPERATORS
        const needsValue = !NO_VALUE_OPERATORS.has(predicate.operator)
        const isMulti = MULTI_VALUE_OPERATORS.has(predicate.operator)

        return (
          <div className="condition-builder-predicate" key={index}>
            <select
              aria-label="الحقل"
              value={predicate.fieldKey}
              onChange={(e) => {
                const nextField = selectableFields.find((f) => f.key === e.target.value)
                const nextOperators = nextField ? operatorsForType(nextField.type) : TEXT_OPERATORS
                updatePredicate(index, { fieldKey: e.target.value, operator: nextOperators[0], value: '', values: undefined })
              }}
            >
              {!field && <option value={predicate.fieldKey}>{predicate.fieldKey} (حقل محذوف)</option>}
              {selectableFields.map((f) => (
                <option key={f.key} value={f.key}>{f.labelAr}</option>
              ))}
            </select>

            <select
              aria-label="المعامل"
              value={predicate.operator}
              onChange={(e) => updatePredicate(index, { operator: Number(e.target.value) as FormConditionOperator, value: '', values: undefined })}
            >
              {operators.map((op) => (
                <option key={op} value={op}>{OPERATOR_LABELS_AR[op]}</option>
              ))}
            </select>

            {needsValue && isMulti && (
              <select
                multiple
                aria-label="القيم"
                value={predicate.values ?? []}
                onChange={(e) =>
                  updatePredicate(index, { values: Array.from(e.target.selectedOptions).map((o) => o.value) })
                }
              >
                {(field?.choiceOptions ?? []).map((opt) => (
                  <option key={opt.value} value={opt.value}>{opt.labelAr}</option>
                ))}
              </select>
            )}

            {needsValue && !isMulti && field?.choiceOptions && field.choiceOptions.length > 0 && (
              <select
                aria-label="القيمة"
                value={predicate.value ?? ''}
                onChange={(e) => updatePredicate(index, { value: e.target.value })}
              >
                <option value="">اختر قيمة</option>
                {field.choiceOptions.map((opt) => (
                  <option key={opt.value} value={opt.value}>{opt.labelAr}</option>
                ))}
              </select>
            )}

            {needsValue && !isMulti && !(field?.choiceOptions && field.choiceOptions.length > 0) && (
              <input
                aria-label="القيمة"
                type={field ? inputTypeForField(field.type) : 'text'}
                value={predicate.value ?? ''}
                onChange={(e) => updatePredicate(index, { value: e.target.value })}
              />
            )}

            <button type="button" className="secondary" aria-label="حذف الشرط" onClick={() => removePredicate(index)}>حذف</button>
          </div>
        )
      })}

      {group.groups.map((nested, index) => (
        <div className="condition-builder-nested" key={index}>
          <ConditionBuilder
            value={nested}
            onChange={(next) => updateNestedGroup(index, next)}
            availableFields={availableFields}
            excludeFieldKey={excludeFieldKey}
            depth={depth + 1}
          />
          <button type="button" className="secondary" onClick={() => removeNestedGroup(index)}>حذف المجموعة الفرعية</button>
        </div>
      ))}

      <div className="condition-builder-row">
        <button type="button" className="secondary" onClick={addPredicate} disabled={selectableFields.length === 0}>
          + شرط
        </button>
        {depth < 2 && (
          <button type="button" className="secondary" onClick={addNestedGroup}>+ مجموعة شروط فرعية</button>
        )}
        <button type="button" className="secondary" onClick={() => onChange(null)}>إزالة الشرط بالكامل</button>
      </div>
    </fieldset>
  )
}
