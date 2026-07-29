import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { createEmptySchema, FormFieldTypes, type FormFieldSchema, type FormSchemaDocument } from '../../../forms/designer/schemaTypes'
import { newField } from './studioWorkspaceHelpers'
import { StudioInspector } from './StudioInspector'

function schemaWithChoiceField(): { schema: FormSchemaDocument; field: FormFieldSchema } {
  const schema = createEmptySchema()
  const field = newField(FormFieldTypes.SingleChoice)
  field.choice = {
    options: [
      { value: 'a', labelAr: 'خيار أ', order: 0, isActive: true },
      { value: 'b', labelAr: 'خيار ب', order: 1, isActive: true },
    ],
    allowOther: false,
  }
  schema.pages[0].sections[0].fields = [field]
  return { schema, field }
}

function Harness() {
  const [{ schema, field }, setState] = useState(schemaWithChoiceField)
  return (
    <StudioInspector
      schema={schema}
      page={schema.pages[0]}
      selectedField={schema.pages[0].sections[0].fields.find((f) => f.id === field.id)}
      issues={[]}
      onApplySchema={(next) => setState({ schema: next, field })}
    />
  )
}

describe('StudioInspector choice options', () => {
  it('does not lose focus while typing into an option key input', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    const keyInput = screen.getByLabelText('مفتاح الخيار 1') as HTMLInputElement
    await user.clear(keyInput)
    await user.type(keyInput, 'custom_key')
    expect(keyInput).toHaveValue('custom_key')
    expect(keyInput).toHaveFocus()
  })

  it('does not lose focus while typing into an option label input', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    const labelInput = screen.getByLabelText('نص الخيار 2') as HTMLInputElement
    await user.type(labelInput, ' معدّل')
    expect(labelInput).toHaveValue('خيار ب معدّل')
    expect(labelInput).toHaveFocus()
  })
})
