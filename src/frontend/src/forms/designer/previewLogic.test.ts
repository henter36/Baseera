import { describe, expect, it } from 'vitest'
import { evaluateCondition, evaluateFormula, toDisplayString } from './previewLogic'

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

  it('handles divide-by-zero and formula functions via registries', () => {
    const divideNode = {
      kind: 'binary' as const,
      operator: 3 as const,
      left: { kind: 'constantNumber' as const, value: 10 },
      right: { kind: 'constantNumber' as const, value: 0 },
    }
    expect(evaluateFormula(divideNode, {})).toBeNull()

    const sumNode = {
      kind: 'function' as const,
      function: 2 as const,
      arguments: [
        { kind: 'constantNumber' as const, value: 1 },
        { kind: 'constantNumber' as const, value: 2 },
      ],
    }
    expect(evaluateFormula(sumNode, {})).toBe(3)
  })

  it('formats display values without object placeholders', () => {
    expect(toDisplayString(null)).toBe('')
    expect(toDisplayString(undefined)).toBe('')
    expect(toDisplayString({ a: 1 })).toBe('{"a":1}')
    expect(toDisplayString([1, 2])).toBe('[1,2]')
    expect(toDisplayString('text')).toBe('text')
    expect(toDisplayString(42)).toBe('42')
    expect(toDisplayString(true)).toBe('true')
    expect(toDisplayString(Symbol('s'))).toBe('Symbol(s)')
    expect(toDisplayString(() => 1)).toBe('')

    const circular: Record<string, unknown> = {}
    circular.self = circular
    expect(toDisplayString(circular)).toBe('')
  })

  it('compares objects via display strings without [object Object]', () => {
    const values = { a: { x: 1 } }
    expect(evaluateCondition({
      combinator: 0,
      predicates: [{ fieldKey: 'a', operator: 0, value: '{"x":1}' }],
      groups: [],
    }, values)).toBe(true)

    const concatNode = {
      kind: 'function' as const,
      function: 9 as const,
      arguments: [
        { kind: 'fieldReference' as const, fieldKey: 'a' },
        { kind: 'constantText' as const, value: '-ok' },
      ],
    }
    const joined = evaluateFormula(concatNode, values)
    expect(String(joined)).not.toContain('[object Object]')
    expect(joined).toBe('{"x":1}-ok')
  })
})
