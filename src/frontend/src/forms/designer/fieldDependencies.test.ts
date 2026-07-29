import { describe, expect, it } from 'vitest'
import { detectDependencyCycle, fieldOwnDependencies, findDependents, flattenFields, wouldCreateSelfReference } from './fieldDependencies'
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

describe('flattenFields', () => {
  it('collects fields across all pages and sections', () => {
    const schema = schemaWithFields([baseField({ key: 'a' }), baseField({ key: 'b' })])
    expect(flattenFields(schema)).toHaveLength(2)
  })
})

describe('fieldOwnDependencies', () => {
  it('collects field keys referenced by visibility, required conditions, and formulas', () => {
    const field = baseField({
      key: 'total',
      visibilityCondition: { combinator: 0, predicates: [{ fieldKey: 'a', operator: 0, value: '1' }], groups: [] },
      requiredCondition: { combinator: 0, predicates: [{ fieldKey: 'b', operator: 9 }], groups: [] },
      formula: { kind: 'binary', operator: 0, left: { kind: 'fieldReference', fieldKey: 'c' }, right: { kind: 'constantNumber', value: 1 } },
    })
    expect(fieldOwnDependencies(field).sort()).toEqual(['a', 'b', 'c'])
  })
})

describe('findDependents', () => {
  it('finds fields whose condition or formula references the given key', () => {
    const dependent = baseField({
      key: 'shown_if_a',
      visibilityCondition: { combinator: 0, predicates: [{ fieldKey: 'a', operator: 9 }], groups: [] },
    })
    const unrelated = baseField({ key: 'other' })
    const schema = schemaWithFields([baseField({ key: 'a' }), dependent, unrelated])

    const dependents = findDependents(schema, 'a')
    expect(dependents).toHaveLength(1)
    expect(dependents[0].field.key).toBe('shown_if_a')
    expect(dependents[0].via).toBe('visibilityCondition')
  })

  it('is case-insensitive on field keys', () => {
    const dependent = baseField({
      key: 'x',
      formula: { kind: 'fieldReference', fieldKey: 'A' },
    })
    const schema = schemaWithFields([baseField({ key: 'a' }), dependent])
    expect(findDependents(schema, 'a')).toHaveLength(1)
  })
})

describe('detectDependencyCycle', () => {
  it('returns null when there is no cycle', () => {
    const a = baseField({ key: 'a' })
    const b = baseField({ key: 'b', formula: { kind: 'fieldReference', fieldKey: 'a' } })
    const schema = schemaWithFields([a, b])
    expect(detectDependencyCycle(schema)).toBeNull()
  })

  it('detects a direct two-field cycle (a depends on b, b depends on a)', () => {
    const a = baseField({ key: 'a', formula: { kind: 'fieldReference', fieldKey: 'b' } })
    const b = baseField({ key: 'b', formula: { kind: 'fieldReference', fieldKey: 'a' } })
    const schema = schemaWithFields([a, b])
    expect(detectDependencyCycle(schema)).not.toBeNull()
  })

  it('detects a self-reference as a cycle', () => {
    const a = baseField({ key: 'a', formula: { kind: 'fieldReference', fieldKey: 'a' } })
    const schema = schemaWithFields([a])
    expect(detectDependencyCycle(schema)).not.toBeNull()
  })

  it('detects a longer transitive cycle (a -> b -> c -> a)', () => {
    const a = baseField({ key: 'a', formula: { kind: 'fieldReference', fieldKey: 'b' } })
    const b = baseField({ key: 'b', formula: { kind: 'fieldReference', fieldKey: 'c' } })
    const c = baseField({ key: 'c', formula: { kind: 'fieldReference', fieldKey: 'a' } })
    const schema = schemaWithFields([a, b, c])
    expect(detectDependencyCycle(schema)).not.toBeNull()
  })
})

describe('wouldCreateSelfReference', () => {
  it('matches case-insensitively and ignores surrounding whitespace', () => {
    expect(wouldCreateSelfReference('  Total ', 'total')).toBe(true)
    expect(wouldCreateSelfReference('total', 'other')).toBe(false)
  })
})
