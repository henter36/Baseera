import { describe, expect, it } from 'vitest'
import { diffSchemas } from './versionDiff'
import { createEmptySchema, type FormFieldSchema, type FormSchemaDocument } from './schemaTypes'

function baseField(overrides: Partial<FormFieldSchema>): FormFieldSchema {
  return {
    id: overrides.id ?? crypto.randomUUID(),
    key: overrides.key ?? 'field',
    type: 0,
    labelAr: 'حقل',
    order: 0,
    layoutWidth: 0,
    isRequired: false,
    validationRules: [],
    isReadOnly: false,
    isCalculated: false,
    ...overrides,
  }
}

function schemaWithFields(fields: FormFieldSchema[]): FormSchemaDocument {
  const schema = createEmptySchema()
  schema.pages[0].sections[0].fields = fields
  return schema
}

describe('diffSchemas', () => {
  it('flags a field present only in the after schema as added', () => {
    const before = schemaWithFields([])
    const after = schemaWithFields([baseField({ key: 'new_field' })])
    const diff = diffSchemas(before, after)
    const entry = diff.find((e) => e.key === 'new_field')
    expect(entry?.kind).toBe('added')
  })

  it('flags a field present only in the before schema as removed', () => {
    const before = schemaWithFields([baseField({ key: 'gone' })])
    const after = schemaWithFields([])
    const diff = diffSchemas(before, after)
    expect(diff.find((e) => e.key === 'gone')?.kind).toBe('removed')
  })

  it('flags a changed label as modified with the property named', () => {
    const before = schemaWithFields([baseField({ key: 'f', labelAr: 'قديم' })])
    const after = schemaWithFields([baseField({ key: 'f', labelAr: 'جديد' })])
    const diff = diffSchemas(before, after)
    const entry = diff.find((e) => e.key === 'f')
    expect(entry?.kind).toBe('modified')
    expect(entry?.changedProperties).toContain('العنوان')
  })

  it('flags identical fields as unchanged', () => {
    const field = baseField({ key: 'stable' })
    const before = schemaWithFields([field])
    const after = schemaWithFields([{ ...field }])
    const diff = diffSchemas(before, after)
    expect(diff.find((e) => e.key === 'stable')?.kind).toBe('unchanged')
  })

  it('reports added/removed/modified option changes', () => {
    const before = schemaWithFields([
      baseField({ key: 'choice', choice: { options: [{ value: 'a', labelAr: 'أ', order: 0, isActive: true }], allowOther: false } }),
    ])
    const after = schemaWithFields([
      baseField({ key: 'choice', choice: { options: [{ value: 'b', labelAr: 'ب', order: 0, isActive: true }], allowOther: false } }),
    ])
    const entry = diffSchemas(before, after).find((e) => e.key === 'choice')
    expect(entry?.optionChanges.some((c) => c.includes('محذوف'))).toBe(true)
    expect(entry?.optionChanges.some((c) => c.includes('مضاف'))).toBe(true)
  })

  it('flags a required-condition-only change as requiredChanged without a required flag flip', () => {
    const before = schemaWithFields([baseField({ key: 'f', isRequired: false })])
    const after = schemaWithFields([
      baseField({ key: 'f', isRequired: false, requiredCondition: { combinator: 0, predicates: [{ fieldKey: 'x', operator: 9 }], groups: [] } }),
    ])
    const entry = diffSchemas(before, after).find((e) => e.key === 'f')
    expect(entry?.requiredChanged).toBe(true)
  })

  it('reports a renamed field (key change) as a removed field plus a new added field, since diffing is keyed by field key', () => {
    const before = schemaWithFields([baseField({ key: 'old_key', labelAr: 'حقل' })])
    const after = schemaWithFields([baseField({ key: 'new_key', labelAr: 'حقل' })])
    const diff = diffSchemas(before, after)
    expect(diff.find((e) => e.key === 'old_key')?.kind).toBe('removed')
    expect(diff.find((e) => e.key === 'new_key')?.kind).toBe('added')
  })

  it('flags changed validation rules', () => {
    const before = schemaWithFields([baseField({ key: 'f', validationRules: [] })])
    const after = schemaWithFields([
      baseField({ key: 'f', validationRules: [{ code: 'custom', messageAr: 'رسالة تحقق' }] }),
    ])
    const entry = diffSchemas(before, after).find((e) => e.key === 'f')
    expect(entry?.kind).toBe('modified')
    expect(entry?.changedProperties).toContain('قواعد التحقق')
  })

  it('flags a changed formula', () => {
    const before = schemaWithFields([baseField({ key: 'total', isCalculated: true, formula: { kind: 'constantNumber', value: 1 } })])
    const after = schemaWithFields([baseField({ key: 'total', isCalculated: true, formula: { kind: 'constantNumber', value: 2 } })])
    const entry = diffSchemas(before, after).find((e) => e.key === 'total')
    expect(entry?.formulaChanged).toBe(true)
  })

  it('flags a changed visibility condition', () => {
    const before = schemaWithFields([baseField({ key: 'f' })])
    const after = schemaWithFields([
      baseField({ key: 'f', visibilityCondition: { combinator: 0, predicates: [{ fieldKey: 'x', operator: 9 }], groups: [] } }),
    ])
    const entry = diffSchemas(before, after).find((e) => e.key === 'f')
    expect(entry?.conditionChanged).toBe(true)
  })

  it('does not flag a field as changed merely because its position in the array changed (order-independent diff)', () => {
    const a = baseField({ key: 'a' })
    const b = baseField({ key: 'b' })
    const before = schemaWithFields([a, b])
    const after = schemaWithFields([{ ...b }, { ...a }])
    const diff = diffSchemas(before, after)
    expect(diff.find((e) => e.key === 'a')?.kind).toBe('unchanged')
    expect(diff.find((e) => e.key === 'b')?.kind).toBe('unchanged')
  })

  it('reports every field as unchanged for an identical (unmodified) form', () => {
    const fields = [baseField({ key: 'a' }), baseField({ key: 'b' })]
    const before = schemaWithFields(fields)
    const after = schemaWithFields(fields.map((f) => ({ ...f })))
    const diff = diffSchemas(before, after)
    expect(diff.every((e) => e.kind === 'unchanged')).toBe(true)
  })
})
