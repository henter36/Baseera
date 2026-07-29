import { flattenFields } from './fieldDependencies'
import type { FormFieldSchema, FormSchemaDocument } from './schemaTypes'

export type FieldDiffKind = 'added' | 'removed' | 'modified' | 'unchanged'

export type FieldDiffEntry = {
  key: string
  kind: FieldDiffKind
  before: FormFieldSchema | null
  after: FormFieldSchema | null
  changedProperties: string[]
  optionChanges: string[]
  conditionChanged: boolean
  formulaChanged: boolean
  requiredChanged: boolean
}

function stableJson(value: unknown): string {
  return JSON.stringify(value ?? null)
}

function diffOptions(before: FormFieldSchema | null, after: FormFieldSchema | null): string[] {
  const beforeOptions = before?.choice?.options ?? []
  const afterOptions = after?.choice?.options ?? []
  const beforeByValue = new Map(beforeOptions.map((o) => [o.value, o]))
  const afterByValue = new Map(afterOptions.map((o) => [o.value, o]))
  const changes: string[] = []

  for (const [value, option] of afterByValue) {
    if (!beforeByValue.has(value)) {
      changes.push(`خيار مضاف: ${option.labelAr}`)
    } else if (stableJson(beforeByValue.get(value)) !== stableJson(option)) {
      changes.push(`خيار معدّل: ${option.labelAr}`)
    }
  }
  for (const [value, option] of beforeByValue) {
    if (!afterByValue.has(value)) {
      changes.push(`خيار محذوف: ${option.labelAr}`)
    }
  }

  return changes
}

type FieldPropertyDiffCheck = {
  label: string
  isDifferent: (before: FormFieldSchema, after: FormFieldSchema) => boolean
}

// Order matches the original if-chain exactly — changedProperties must render in this sequence.
const FIELD_PROPERTY_CHECKS: FieldPropertyDiffCheck[] = [
  { label: 'العنوان', isDifferent: (b, a) => b.labelAr !== a.labelAr },
  { label: 'النوع', isDifferent: (b, a) => b.type !== a.type },
  { label: 'الوصف', isDifferent: (b, a) => (b.description ?? '') !== (a.description ?? '') },
  { label: 'القيمة الافتراضية', isDifferent: (b, a) => (b.defaultValue ?? '') !== (a.defaultValue ?? '') },
  { label: 'إعدادات النص', isDifferent: (b, a) => stableJson(b.text) !== stableJson(a.text) },
  { label: 'إعدادات الرقم', isDifferent: (b, a) => stableJson(b.number) !== stableJson(a.number) },
  { label: 'إعدادات المرفق', isDifferent: (b, a) => stableJson(b.file) !== stableJson(a.file) },
  { label: 'قواعد التحقق', isDifferent: (b, a) => stableJson(b.validationRules) !== stableJson(a.validationRules) },
]

function diffFieldProperties(before: FormFieldSchema | null, after: FormFieldSchema | null): string[] {
  if (!before || !after) return []
  return FIELD_PROPERTY_CHECKS.filter((check) => check.isDifferent(before, after)).map((check) => check.label)
}

function hasRequiredChanged(before: FormFieldSchema | null, after: FormFieldSchema | null): boolean {
  const requiredFlagChanged = (before?.isRequired ?? false) !== (after?.isRequired ?? false)
  const requiredConditionChanged = stableJson(before?.requiredCondition) !== stableJson(after?.requiredCondition)
  return requiredFlagChanged || requiredConditionChanged
}

function diffField(before: FormFieldSchema | null, after: FormFieldSchema | null): Omit<FieldDiffEntry, 'key' | 'kind' | 'before' | 'after'> {
  return {
    changedProperties: diffFieldProperties(before, after),
    optionChanges: diffOptions(before, after),
    conditionChanged: stableJson(before?.visibilityCondition) !== stableJson(after?.visibilityCondition),
    formulaChanged: stableJson(before?.formula) !== stableJson(after?.formula),
    requiredChanged: hasRequiredChanged(before, after),
  }
}

export function diffSchemas(before: FormSchemaDocument, after: FormSchemaDocument): FieldDiffEntry[] {
  const beforeFields = new Map(flattenFields(before).map(({ field }) => [field.key.toLowerCase(), field]))
  const afterFields = new Map(flattenFields(after).map(({ field }) => [field.key.toLowerCase(), field]))
  const allKeys = new Set([...beforeFields.keys(), ...afterFields.keys()])
  const entries: FieldDiffEntry[] = []

  for (const key of allKeys) {
    const beforeField = beforeFields.get(key) ?? null
    const afterField = afterFields.get(key) ?? null
    const detail = diffField(beforeField, afterField)
    const hasFieldChanges = detail.changedProperties.length > 0
      || detail.optionChanges.length > 0
      || detail.conditionChanged
      || detail.formulaChanged
      || detail.requiredChanged

    let kind: FieldDiffKind = 'unchanged'
    if (!beforeField) kind = 'added'
    else if (!afterField) kind = 'removed'
    else if (hasFieldChanges) kind = 'modified'

    entries.push({ key, kind, before: beforeField, after: afterField, ...detail })
  }

  return entries.sort((a, b) => a.key.localeCompare(b.key))
}
