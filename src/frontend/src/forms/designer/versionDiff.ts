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

function diffField(before: FormFieldSchema | null, after: FormFieldSchema | null): Omit<FieldDiffEntry, 'key' | 'kind' | 'before' | 'after'> {
  const changedProperties: string[] = []
  if (before && after) {
    if (before.labelAr !== after.labelAr) changedProperties.push('العنوان')
    if (before.type !== after.type) changedProperties.push('النوع')
    if ((before.description ?? '') !== (after.description ?? '')) changedProperties.push('الوصف')
    if ((before.defaultValue ?? '') !== (after.defaultValue ?? '')) changedProperties.push('القيمة الافتراضية')
    if (stableJson(before.text) !== stableJson(after.text)) changedProperties.push('إعدادات النص')
    if (stableJson(before.number) !== stableJson(after.number)) changedProperties.push('إعدادات الرقم')
    if (stableJson(before.file) !== stableJson(after.file)) changedProperties.push('إعدادات المرفق')
  }

  return {
    changedProperties,
    optionChanges: diffOptions(before, after),
    conditionChanged: stableJson(before?.visibilityCondition) !== stableJson(after?.visibilityCondition),
    formulaChanged: stableJson(before?.formula) !== stableJson(after?.formula),
    requiredChanged: (before?.isRequired ?? false) !== (after?.isRequired ?? false)
      || stableJson(before?.requiredCondition) !== stableJson(after?.requiredCondition),
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
