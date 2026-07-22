import { describe, expect, it } from 'vitest'
import { renameFieldKeyInSchema } from './designerHelpers'
import { FormFieldTypes, type FormSchemaDocument } from './schemaTypes'

const baseSchema: FormSchemaDocument = {
  schemaFormatVersion: 1,
  pages: [{
    id: 'p1',
    key: 'page1',
    titleAr: 'صفحة',
    order: 0,
    visibilityCondition: {
      combinator: 0,
      predicates: [{ fieldKey: 'source', operator: 0, value: 'yes' }],
      groups: [],
    },
    sections: [{
      id: 's1',
      key: 'section1',
      titleAr: 'قسم',
      order: 0,
      fields: [
        {
          id: 'f1',
          key: 'source',
          type: FormFieldTypes.ShortText,
          labelAr: 'مصدر',
          order: 0,
          layoutWidth: 0,
          isRequired: false,
          validationRules: [],
          isReadOnly: false,
          isCalculated: false,
        },
        {
          id: 'f2',
          key: 'target',
          type: FormFieldTypes.CalculatedNumber,
          labelAr: 'هدف',
          order: 1,
          layoutWidth: 0,
          isRequired: false,
          validationRules: [],
          isReadOnly: true,
          isCalculated: true,
          visibilityCondition: {
            combinator: 0,
            predicates: [{ fieldKey: 'source', operator: 9 }],
            groups: [],
          },
          requiredCondition: {
            combinator: 0,
            predicates: [{ fieldKey: 'source', operator: 0, value: 'yes' }],
            groups: [],
          },
          formula: {
            kind: 'binary',
            operator: 0,
            left: { kind: 'fieldReference', fieldKey: 'source' },
            right: { kind: 'constantNumber', value: 1 },
          },
        },
        {
          id: 'f3',
          key: 'other',
          type: FormFieldTypes.ShortText,
          labelAr: 'آخر',
          order: 2,
          layoutWidth: 0,
          isRequired: false,
          validationRules: [],
          isReadOnly: false,
          isCalculated: false,
        },
      ],
    }],
  }],
}

describe('renameFieldKeyInSchema', () => {
  it('rejects empty and invalid keys', () => {
    expect(renameFieldKeyInSchema(baseSchema, 'f1', '').ok).toBe(false)
    expect(renameFieldKeyInSchema(baseSchema, 'f1', '   ').ok).toBe(false)
    expect(renameFieldKeyInSchema(baseSchema, 'f1', '1bad').ok).toBe(false)
  })

  it('rejects case-insensitive duplicates', () => {
    const result = renameFieldKeyInSchema(baseSchema, 'f1', 'TARGET')
    expect(result.ok).toBe(false)
    if (!result.ok) {
      expect(result.error).toContain('مكرر')
    }
  })

  it('updates field key and all references atomically', () => {
    const result = renameFieldKeyInSchema(baseSchema, 'f1', 'origin')
    expect(result.ok).toBe(true)
    if (!result.ok) {
      return
    }

    const renamedField = result.schema.pages[0].sections[0].fields.find((field) => field.id === 'f1')
    expect(renamedField?.key).toBe('origin')
    expect(result.schema.pages[0].visibilityCondition?.predicates[0].fieldKey).toBe('origin')

    const dependent = result.schema.pages[0].sections[0].fields.find((field) => field.id === 'f2')
    expect(dependent?.visibilityCondition?.predicates[0].fieldKey).toBe('origin')
    expect(dependent?.requiredCondition?.predicates[0].fieldKey).toBe('origin')
    expect(dependent?.formula).toEqual({
      kind: 'binary',
      operator: 0,
      left: { kind: 'fieldReference', fieldKey: 'origin' },
      right: { kind: 'constantNumber', value: 1 },
    })
  })

  it('renames repeating-table columns and excludes self from duplicates', () => {
    const schema: FormSchemaDocument = {
      schemaFormatVersion: 1,
      pages: [{
        id: 'p1',
        key: 'page1',
        titleAr: 'صفحة',
        order: 0,
        sections: [{
          id: 's1',
          key: 'section1',
          titleAr: 'قسم',
          order: 0,
          fields: [{
            id: 'table',
            key: 'table',
            type: FormFieldTypes.RepeatingTable,
            labelAr: 'جدول',
            order: 0,
            layoutWidth: 0,
            isRequired: false,
            validationRules: [],
            isReadOnly: false,
            isCalculated: false,
            repeatingTable: {
              minRows: 0,
              maxRows: 5,
              columns: [
                {
                  id: 'c1',
                  key: 'colA',
                  type: FormFieldTypes.ShortText,
                  labelAr: 'أ',
                  order: 0,
                  layoutWidth: 0,
                  isRequired: false,
                  validationRules: [],
                  isReadOnly: false,
                  isCalculated: false,
                  requiredCondition: {
                    combinator: 0,
                    predicates: [{ fieldKey: 'colA', operator: 9 }],
                    groups: [],
                  },
                },
                {
                  id: 'c2',
                  key: 'colB',
                  type: FormFieldTypes.ShortText,
                  labelAr: 'ب',
                  order: 1,
                  layoutWidth: 0,
                  isRequired: false,
                  validationRules: [],
                  isReadOnly: false,
                  isCalculated: false,
                },
              ],
            },
          }],
        }],
      }],
    }

    const sameCase = renameFieldKeyInSchema(schema, 'c1', 'colA')
    expect(sameCase.ok).toBe(true)

    const duplicate = renameFieldKeyInSchema(schema, 'c1', 'colB')
    expect(duplicate.ok).toBe(false)

    const renamed = renameFieldKeyInSchema(schema, 'c1', 'colOrigin')
    expect(renamed.ok).toBe(true)
    if (!renamed.ok) {
      return
    }

    const column = renamed.schema.pages[0].sections[0].fields[0].repeatingTable?.columns.find((item) => item.id === 'c1')
    expect(column?.key).toBe('colOrigin')
    expect(column?.requiredCondition?.predicates[0].fieldKey).toBe('colOrigin')
  })
})
