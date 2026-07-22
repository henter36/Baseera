export const FormFieldTypes = {
  ShortText: 0,
  LongText: 1,
  Number: 2,
  Percentage: 3,
  Date: 4,
  Time: 5,
  DateTime: 6,
  SingleChoice: 7,
  MultipleChoice: 8,
  YesNo: 9,
  File: 10,
  Image: 11,
  Signature: 12,
  Location: 13,
  RepeatingTable: 14,
  OrganizationalReference: 15,
  CalculatedNumber: 16,
  CalculatedText: 17,
} as const

export type FormFieldType = (typeof FormFieldTypes)[keyof typeof FormFieldTypes]

export const FormFieldTypeLabelsAr: Record<number, string> = {
  0: 'نص قصير',
  1: 'نص طويل',
  2: 'رقم',
  3: 'نسبة',
  4: 'تاريخ',
  5: 'وقت',
  6: 'تاريخ ووقت',
  7: 'اختيار واحد',
  8: 'اختيار متعدد',
  9: 'نعم/لا',
  10: 'ملف',
  11: 'صورة',
  12: 'توقيع',
  13: 'موقع',
  14: 'جدول متكرر',
  15: 'مرجع تنظيمي',
  16: 'رقم محسوب',
  17: 'نص محسوب',
}

export type FormConditionOperator =
  | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15

export type FormConditionGroup = {
  combinator: 0 | 1
  predicates: Array<{
    fieldKey: string
    operator: FormConditionOperator
    value?: string | null
    values?: string[] | null
  }>
  groups: FormConditionGroup[]
}

export type FormFormulaNode =
  | { kind: 'constantNumber'; value: number }
  | { kind: 'constantText'; value: string }
  | { kind: 'fieldReference'; fieldKey: string }
  | { kind: 'binary'; operator: 0 | 1 | 2 | 3 | 4; left: FormFormulaNode; right: FormFormulaNode }
  | { kind: 'function'; function: number; arguments: FormFormulaNode[] }

export type FormFieldOption = {
  value: string
  labelAr: string
  labelEn?: string | null
  order: number
  isActive: boolean
}

export type FormFieldSchema = {
  id: string
  key: string
  type: FormFieldType
  labelAr: string
  labelEn?: string | null
  description?: string | null
  instructions?: string | null
  placeholder?: string | null
  order: number
  layoutWidth: number
  isRequired: boolean
  defaultValue?: string | null
  visibilityCondition?: FormConditionGroup | null
  requiredCondition?: FormConditionGroup | null
  validationRules: Array<{ code: string; messageAr: string; messageEn?: string | null }>
  classificationOverride?: number | null
  isReadOnly: boolean
  isCalculated: boolean
  text?: {
    minLength?: number | null
    maxLength?: number | null
    kind: number
    customPattern?: string | null
  } | null
  number?: {
    min?: number | null
    max?: number | null
    decimalPlaces?: number | null
    step?: number | null
    unit?: string | null
  } | null
  choice?: {
    options: FormFieldOption[]
    allowOther: boolean
    minSelections?: number | null
    maxSelections?: number | null
  } | null
  file?: {
    maxFiles: number
    maxFileSizeBytes: number
    allowedMimeTypes: string[]
    allowedExtensions: string[]
    requireVirusScan: boolean
  } | null
  repeatingTable?: {
    minRows: number
    maxRows: number
    columns: FormFieldSchema[]
  } | null
  organizationalReference?: { kind: number } | null
  formula?: FormFormulaNode | null
}

export type FormSectionSchema = {
  id: string
  key: string
  titleAr: string
  titleEn?: string | null
  description?: string | null
  order: number
  visibilityCondition?: FormConditionGroup | null
  fields: FormFieldSchema[]
}

export type FormPageSchema = {
  id: string
  key: string
  titleAr: string
  titleEn?: string | null
  description?: string | null
  order: number
  visibilityCondition?: FormConditionGroup | null
  sections: FormSectionSchema[]
}

export type FormSchemaDocument = {
  schemaFormatVersion: number
  pages: FormPageSchema[]
}

export function createEmptySchema(): FormSchemaDocument {
  const pageId = crypto.randomUUID()
  const sectionId = crypto.randomUUID()
  const fieldId = crypto.randomUUID()
  return {
    schemaFormatVersion: 1,
    pages: [
      {
        id: pageId,
        key: 'page1',
        titleAr: 'الصفحة 1',
        order: 0,
        sections: [
          {
            id: sectionId,
            key: 'section1',
            titleAr: 'القسم 1',
            order: 0,
            fields: [
              {
                id: fieldId,
                key: 'field1',
                type: FormFieldTypes.ShortText,
                labelAr: 'حقل نصي',
                order: 0,
                layoutWidth: 0,
                isRequired: false,
                validationRules: [],
                isReadOnly: false,
                isCalculated: false,
              },
            ],
          },
        ],
      },
    ],
  }
}

export function reindexOrders(schema: FormSchemaDocument): FormSchemaDocument {
  return {
    ...schema,
    pages: schema.pages.map((page, pi) => ({
      ...page,
      order: pi,
      sections: page.sections.map((section, si) => ({
        ...section,
        order: si,
        fields: section.fields.map((field, fi) => ({ ...field, order: fi })),
      })),
    })),
  }
}

export function cloneSchema(schema: FormSchemaDocument): FormSchemaDocument {
  return structuredClone(schema)
}
