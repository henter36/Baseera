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
})
