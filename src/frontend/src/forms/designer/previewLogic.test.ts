import { describe, expect, it } from 'vitest'
import { evaluateCondition, evaluateFormula } from './previewLogic'

describe('preview logic', () => {
  it('evaluates all/any conditions', () => {
    const values = { a: 'x', b: '' }
    expect(evaluateCondition({
      combinator: 0,
      predicates: [
        { fieldKey: 'a', operator: 0, value: 'x' },
        { fieldKey: 'b', operator: 8 },
      ],
      groups: [],
    }, values)).toBe(true)
  })

  it('evaluates formula nodes safely', () => {
    const node = {
      kind: 'binary' as const,
      operator: 0 as const,
      left: { kind: 'constantNumber' as const, value: 2 },
      right: { kind: 'fieldReference' as const, fieldKey: 'n' },
    }
    expect(evaluateFormula(node, { n: 3 })).toBe(5)
  })
})
