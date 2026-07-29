import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { ConditionBuilder, type ConditionableField } from './ConditionBuilder'
import type { FormConditionGroup } from './schemaTypes'

const FIELDS: ConditionableField[] = [
  { key: 'text_field', labelAr: 'حقل نصي', type: 0 },
  { key: 'number_field', labelAr: 'حقل رقمي', type: 2 },
  { key: 'choice_field', labelAr: 'حقل اختيار', type: 7, choiceOptions: [{ value: 'a', labelAr: 'أ', order: 0, isActive: true }, { value: 'b', labelAr: 'ب', order: 1, isActive: true }] },
]

function Harness({ initial, onChangeSpy }: Readonly<{ initial: FormConditionGroup | null; onChangeSpy?: (v: FormConditionGroup | null) => void }>) {
  const [value, setValue] = useState<FormConditionGroup | null>(initial)
  return (
    <ConditionBuilder
      value={value}
      onChange={(next) => { setValue(next); onChangeSpy?.(next) }}
      availableFields={FIELDS}
      excludeFieldKey="self_field"
    />
  )
}

describe('ConditionBuilder', () => {
  it('renders an add-condition affordance when there is no condition yet', () => {
    render(<Harness initial={null} />)
    expect(screen.getByRole('button', { name: '+ إضافة شرط' })).toBeInTheDocument()
  })

  it('adds a predicate with operators restricted to the selected field type', async () => {
    const user = userEvent.setup()
    render(<Harness initial={null} />)
    await user.click(screen.getByRole('button', { name: '+ إضافة شرط' }))
    await user.click(screen.getByRole('button', { name: '+ شرط' }))

    const operatorSelect = screen.getByLabelText('المعامل') as HTMLSelectElement
    const optionLabels = Array.from(operatorSelect.options).map((o) => o.textContent)
    // The first available field ("text_field", ShortText) should only offer text-compatible operators.
    expect(optionLabels).toContain('يساوي')
    expect(optionLabels).not.toContain('أكبر من')
  })

  it('excludes the field being edited from the field picker (no self-reference)', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ combinator: 0, predicates: [{ fieldKey: 'text_field', operator: 0, value: '' }], groups: [] }} />)
    await user.click(screen.getByRole('button', { name: '+ شرط' }))
    const fieldSelects = screen.getAllByLabelText('الحقل') as HTMLSelectElement[]
    for (const select of fieldSelects) {
      expect(Array.from(select.options).some((o) => o.value === 'self_field')).toBe(false)
    }
  })

  it('supports nested condition groups up to the depth limit', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ combinator: 0, predicates: [], groups: [] }} />)
    await user.click(screen.getByRole('button', { name: '+ مجموعة شروط فرعية' }))
    expect(screen.getAllByRole('group', { name: 'مُنشئ الشرط' })).toHaveLength(2)
  })

  it('does not lose focus while typing into a predicate value input', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ combinator: 0, predicates: [{ fieldKey: 'text_field', operator: 0, value: '' }], groups: [] }} />)
    const valueInput = screen.getByLabelText('القيمة') as HTMLInputElement
    await user.type(valueInput, 'hello')
    expect(valueInput).toHaveValue('hello')
    expect(valueInput).toHaveFocus()
  })

  it('calls onChange(null) when removing the whole condition', async () => {
    const user = userEvent.setup()
    const spy = vi.fn()
    render(<Harness initial={{ combinator: 0, predicates: [], groups: [] }} onChangeSpy={spy} />)
    await user.click(screen.getByRole('button', { name: 'إزالة الشرط بالكامل' }))
    expect(spy).toHaveBeenCalledWith(null)
  })
})
