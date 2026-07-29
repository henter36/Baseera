import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { FormulaBuilder, type FormulaField } from './FormulaBuilder'
import type { FormFormulaNode } from './schemaTypes'

const FIELDS: FormulaField[] = [
  { key: 'qty', labelAr: 'الكمية', type: 2 },
  { key: 'price', labelAr: 'السعر', type: 2 },
  { key: 'note', labelAr: 'ملاحظة', type: 0 },
]

function Harness({ initial }: Readonly<{ initial: FormFormulaNode | null }>) {
  const [value, setValue] = useState<FormFormulaNode | null>(initial)
  return <FormulaBuilder value={value} onChange={setValue} availableFields={FIELDS} excludeFieldKey="total" />
}

describe('FormulaBuilder', () => {
  it('renders an add-formula affordance when there is no formula yet', () => {
    render(<Harness initial={null} />)
    expect(screen.getByRole('button', { name: '+ إضافة صيغة حساب' })).toBeInTheDocument()
  })

  it('starts as a binary operation once added', async () => {
    const user = userEvent.setup()
    render(<Harness initial={null} />)
    await user.click(screen.getByRole('button', { name: '+ إضافة صيغة حساب' }))
    const kindSelects = screen.getAllByLabelText('نوع العنصر') as HTMLSelectElement[]
    expect(kindSelects[0].value).toBe('binary')
  })

  it('warns inline about division by a constant zero', async () => {
    render(
      <Harness
        initial={{ kind: 'binary', operator: 3, left: { kind: 'constantNumber', value: 10 }, right: { kind: 'constantNumber', value: 0 } }}
      />,
    )
    expect(screen.getByRole('alert')).toHaveTextContent('القسمة على صفر غير مسموحة.')
  })

  it('restricts a Min/Max/Sum-style function argument to numeric fields only', async () => {
    render(<Harness initial={{ kind: 'function', function: 2, arguments: [{ kind: 'fieldReference', fieldKey: '' }] }} />)
    const fieldSelect = screen.getByLabelText('الحقل المرجعي') as HTMLSelectElement
    const optionValues = Array.from(fieldSelect.options).map((o) => o.value)
    expect(optionValues).toContain('qty')
    expect(optionValues).not.toContain('note')
  })

  it('allows a text field as a Concat argument', () => {
    render(<Harness initial={{ kind: 'function', function: 9, arguments: [{ kind: 'fieldReference', fieldKey: '' }] }} />)
    const fieldSelect = screen.getByLabelText('الحقل المرجعي') as HTMLSelectElement
    const optionValues = Array.from(fieldSelect.options).map((o) => o.value)
    expect(optionValues).toContain('note')
  })

  it('excludes the field being edited from any field reference picker', () => {
    render(<Harness initial={{ kind: 'fieldReference', fieldKey: '' }} />)
    const fieldSelect = screen.getByLabelText('الحقل المرجعي') as HTMLSelectElement
    expect(Array.from(fieldSelect.options).some((o) => o.value === 'total')).toBe(false)
  })

  it('adds and removes arguments for variadic functions', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ kind: 'function', function: 2, arguments: [{ kind: 'constantNumber', value: 1 }] }} />)
    await user.click(screen.getByRole('button', { name: '+ معامل' }))
    expect(screen.getAllByLabelText('نوع العنصر')).toHaveLength(3) // outer + 2 arguments
    await user.click(screen.getAllByRole('button', { name: 'حذف المعامل' })[0])
    expect(screen.getAllByLabelText('نوع العنصر')).toHaveLength(2)
  })
})
