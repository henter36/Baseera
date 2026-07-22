import { describe, expect, it } from 'vitest'
import {
  createEmptySchema,
  isValidFieldKey,
  reindexChoice,
  reindexField,
  reindexOrders,
  reindexPage,
  reindexRepeatingTable,
  reindexSection,
  type FormFieldSchema,
} from './schemaTypes'

describe('isValidFieldKey', () => {
  it('accepts and rejects field keys per contract', () => {
    expect(isValidFieldKey('field_name1')).toBe(true)
    expect(isValidFieldKey('Field1')).toBe(true)
    expect(isValidFieldKey('_field')).toBe(false)
    expect(isValidFieldKey('1field')).toBe(false)
    expect(isValidFieldKey('field-name')).toBe(false)
    expect(isValidFieldKey('حقل')).toBe(false)
    expect(isValidFieldKey('')).toBe(false)
    expect(isValidFieldKey('   ')).toBe(false)
  })
})

describe('reindexOrders helpers', () => {
  it('reindexes nested orders without mutating the original', () => {
    const schema = createEmptySchema()
    const page = schema.pages[0]
    const section = page.sections[0]
    const field = section.fields[0]

    field.order = 9
    section.order = 8
    page.order = 7

    const reindexed = reindexOrders(schema)
    expect(reindexed.pages[0].order).toBe(0)
    expect(reindexed.pages[0].sections[0].order).toBe(0)
    expect(reindexed.pages[0].sections[0].fields[0].order).toBe(0)
    expect(schema.pages[0].order).toBe(7)
  })

  it('reindexes choice and repeating table columns', () => {
    const field: FormFieldSchema = {
      id: 'f1',
      key: 'table',
      type: 14,
      labelAr: 'جدول',
      order: 4,
      layoutWidth: 0,
      isRequired: false,
      validationRules: [],
      isReadOnly: false,
      isCalculated: false,
      choice: {
        options: [
          { value: 'a', labelAr: 'أ', order: 5, isActive: true },
          { value: 'b', labelAr: 'ب', order: 6, isActive: true },
        ],
        allowOther: false,
      },
      repeatingTable: {
        minRows: 0,
        maxRows: 10,
        columns: [
          {
            id: 'c1',
            key: 'col1',
            type: 0,
            labelAr: 'عمود',
            order: 3,
            layoutWidth: 0,
            isRequired: false,
            validationRules: [],
            isReadOnly: false,
            isCalculated: false,
          },
        ],
      },
    }

    const reindexedField = reindexField(field, 0)
    expect(reindexChoice(field.choice!).options.map((option) => option.order)).toEqual([0, 1])
    expect(reindexRepeatingTable(field.repeatingTable!).columns[0].order).toBe(0)
    expect(reindexedField.order).toBe(0)
    expect(field.order).toBe(4)
    expect(field.choice!.options[0].order).toBe(5)
  })

  it('coordinates page and section reindexing', () => {
    const schema = createEmptySchema()
    const reindexedPage = reindexPage(schema.pages[0], 2)
    const reindexedSection = reindexSection(schema.pages[0].sections[0], 4)

    expect(reindexedPage.order).toBe(2)
    expect(reindexedSection.order).toBe(4)
    expect(reindexOrders(schema).pages[0].sections[0].fields[0].order).toBe(0)
  })
})
