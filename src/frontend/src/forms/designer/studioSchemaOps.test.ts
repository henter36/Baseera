import { describe, expect, it } from 'vitest'
import { duplicateField, duplicatePage, duplicateSection } from './studioSchemaOps'
import type { FormFieldSchema, FormSchemaDocument } from './schemaTypes'

function textField(key: string, id: string): FormFieldSchema {
  return {
    id,
    key,
    type: 0,
    labelAr: key,
    order: 0,
    layoutWidth: 0,
    isRequired: false,
    validationRules: [],
    isReadOnly: false,
    isCalculated: false,
  }
}

function schemaWithTwoFieldsInOneSection(): FormSchemaDocument {
  const driver = textField('driver', 'field-driver')
  const dependent = {
    ...textField('dependent', 'field-dependent'),
    visibilityCondition: { combinator: 0 as const, predicates: [{ fieldKey: 'driver', operator: 0 as const, value: 'x' }], groups: [] },
  }
  return {
    schemaFormatVersion: 1,
    pages: [{
      id: 'page-1',
      key: 'page1',
      titleAr: 'الصفحة 1',
      order: 0,
      sections: [{ id: 'section-1', key: 'section1', titleAr: 'القسم 1', order: 0, fields: [driver, dependent] }],
    }],
  }
}

describe('duplicateSection', () => {
  it('rewrites a visibility condition to reference the duplicated driver field, not the original', () => {
    const result = duplicateSection(schemaWithTwoFieldsInOneSection(), 'page-1', 'section-1')
    const copiedSection = result.pages[0].sections[1]
    const copiedDriverKey = copiedSection.fields.find((f) => f.labelAr === 'driver')!.key
    const copiedDependent = copiedSection.fields.find((f) => f.labelAr === 'dependent')!

    expect(copiedDriverKey).not.toBe('driver')
    expect(copiedDependent.visibilityCondition?.predicates[0].fieldKey).toBe(copiedDriverKey)
  })
})

describe('duplicatePage', () => {
  it('rewrites a formula field reference across sections within the duplicated page', () => {
    const source = schemaWithTwoFieldsInOneSection()
    const calc = textField('total', 'field-total')
    calc.formula = { kind: 'fieldReference', fieldKey: 'driver' }
    source.pages[0].sections.push({ id: 'section-2', key: 'section2', titleAr: 'القسم 2', order: 1, fields: [calc] })

    const result = duplicatePage(source, 'page-1')
    const copiedPage = result.pages[1]
    const copiedDriverKey = copiedPage.sections[0].fields.find((f) => f.labelAr === 'driver')!.key
    const copiedCalc = copiedPage.sections[1].fields.find((f) => f.labelAr === 'total')!

    expect(copiedCalc.formula).toEqual({ kind: 'fieldReference', fieldKey: copiedDriverKey })
  })
})

describe('duplicateField', () => {
  it('leaves a reference to an untouched sibling field pointed at the original key', () => {
    const result = duplicateField(schemaWithTwoFieldsInOneSection(), 'page-1', 'section-1', 'field-dependent')
    const fields = result.pages[0].sections[0].fields
    const copiedDependent = fields[fields.findIndex((f) => f.id === 'field-dependent') + 1]

    expect(copiedDependent.visibilityCondition?.predicates[0].fieldKey).toBe('driver')
  })

  it('rewrites internal repeating-table column conditions to the duplicated columns', () => {
    const columnA = textField('col_a', 'col-a')
    const columnB = {
      ...textField('col_b', 'col-b'),
      visibilityCondition: { combinator: 0 as const, predicates: [{ fieldKey: 'col_a', operator: 0 as const, value: 'x' }], groups: [] },
    }
    const table = {
      ...textField('table', 'field-table'),
      repeatingTable: { minRows: 0, maxRows: 10, columns: [columnA, columnB] },
    }
    const schema: FormSchemaDocument = {
      schemaFormatVersion: 1,
      pages: [{
        id: 'page-1', key: 'page1', titleAr: 'ص', order: 0,
        sections: [{ id: 'section-1', key: 'section1', titleAr: 'ق', order: 0, fields: [table] }],
      }],
    }

    const result = duplicateField(schema, 'page-1', 'section-1', 'field-table')
    const fields = result.pages[0].sections[0].fields
    const copiedTable = fields[fields.findIndex((f) => f.id === 'field-table') + 1]
    const copiedColumnA = copiedTable.repeatingTable!.columns.find((c) => c.labelAr === 'col_a')!
    const copiedColumnB = copiedTable.repeatingTable!.columns.find((c) => c.labelAr === 'col_b')!

    expect(copiedColumnA.key).not.toBe('col_a')
    expect(copiedColumnB.visibilityCondition?.predicates[0].fieldKey).toBe(copiedColumnA.key)
  })
})
