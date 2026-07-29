import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { classifyIssues, computeSuggestions, locateIssue, ValidationPanel } from './ValidationPanel'
import { createEmptySchema, type FormFieldSchema, type FormSchemaDocument } from './schemaTypes'
import type { FormSchemaValidationIssue } from '../../api/client'

function schemaWithOneField(overrides: Partial<FormFieldSchema> = {}): FormSchemaDocument {
  const schema = createEmptySchema()
  schema.pages[0].sections[0].fields[0] = { ...schema.pages[0].sections[0].fields[0], ...overrides }
  return schema
}

describe('classifyIssues', () => {
  it('splits issues into errors (severity 0) and warnings (severity != 0)', () => {
    const schema = schemaWithOneField()
    const issues: FormSchemaValidationIssue[] = [
      { code: 'E1', path: 'p', messageAr: 'خطأ', severity: 0 },
      { code: 'W1', path: 'p', messageAr: 'تحذير', severity: 1 },
    ]
    const { errors, warnings } = classifyIssues(schema, issues)
    expect(errors).toHaveLength(1)
    expect(warnings).toHaveLength(1)
  })

  it('never surfaces the raw error code as the user-facing message', () => {
    const schema = schemaWithOneField()
    const { errors } = classifyIssues(schema, [{ code: 'DuplicateFieldKey', path: 'p', messageAr: 'مفتاح مكرر في النموذج.', severity: 0 }])
    expect(errors[0].messageAr).not.toContain('DuplicateFieldKey')
    expect(errors[0].actionAr).toBe('غيّر مفتاح الحقل إلى قيمة فريدة.')
  })
})

describe('locateIssue', () => {
  it('resolves a field-scoped issue to its page/section/field context', () => {
    const schema = createEmptySchema()
    const field = schema.pages[0].sections[0].fields[0]
    const location = locateIssue(schema, { code: 'X', path: 'p', fieldKey: field.key, messageAr: 'm', severity: 0 })
    expect(location.fieldId).toBe(field.id)
    expect(location.pageId).toBe(schema.pages[0].id)
  })
})

describe('computeSuggestions', () => {
  it('suggests adding a field when the schema is empty', () => {
    const schema = createEmptySchema()
    schema.pages[0].sections[0].fields = []
    expect(computeSuggestions(schema).some((s) => s.id === 'no-fields')).toBe(true)
  })

  it('suggests a second option for a choice field with fewer than two options', () => {
    const schema = schemaWithOneField({ choice: { options: [{ value: 'a', labelAr: 'أ', order: 0, isActive: true }], allowOther: false } })
    expect(computeSuggestions(schema).some((s) => s.id.startsWith('few-options-'))).toBe(true)
  })
})

describe('ValidationPanel component', () => {
  it('renders counts and lets the user jump to the offending element', async () => {
    const user = userEvent.setup()
    const schema = createEmptySchema()
    const field = schema.pages[0].sections[0].fields[0]
    const onNavigate = vi.fn()
    render(
      <ValidationPanel
        schema={schema}
        issues={[{ code: 'X', path: 'p', fieldKey: field.key, messageAr: 'رسالة خطأ', severity: 0 }]}
        onNavigateToElement={onNavigate}
      />,
    )

    expect(screen.getByText('أخطاء تمنع المراجعة (1)')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'الانتقال إلى العنصر' }))
    expect(onNavigate).toHaveBeenCalledWith(expect.objectContaining({ fieldId: field.id }))
  })
})
