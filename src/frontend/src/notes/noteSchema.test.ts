import { describe, expect, it } from 'vitest'
import { assignNoteSchema, createNoteSchema, updateNoteSchema } from './noteSchema'

const validCreateBase = {
  title: 'ملاحظة تجريبية',
  description: 'وصف تفصيلي للملاحظة التشغيلية.',
  category: '0',
  severity: '1',
  sourceType: '0',
  sourceReference: '',
  classification: '0',
  ownerDepartmentId: '',
  dueAtUtc: '',
}

describe('createNoteSchema', () => {
  it('rejects an empty title', () => {
    const result = createNoteSchema.safeParse({ ...validCreateBase, title: '   ', scopeType: '0' })
    expect(result.success).toBe(false)
  })

  it('rejects when no scope type is selected', () => {
    const result = createNoteSchema.safeParse({ ...validCreateBase, scopeType: '-1' })
    expect(result.success).toBe(false)
  })

  it('accepts Global scope with no region/facility/unit', () => {
    const result = createNoteSchema.safeParse({ ...validCreateBase, scopeType: '0' })
    expect(result.success).toBe(true)
  })

  it('rejects Global scope when a region is also provided', () => {
    const result = createNoteSchema.safeParse({
      ...validCreateBase,
      scopeType: '0',
      regionId: '11111111-1111-1111-1111-111111111111',
    })
    expect(result.success).toBe(false)
  })

  it('requires a regionId for Region scope', () => {
    const result = createNoteSchema.safeParse({ ...validCreateBase, scopeType: '2' })
    expect(result.success).toBe(false)
  })

  it('accepts Region scope with a regionId', () => {
    const result = createNoteSchema.safeParse({
      ...validCreateBase,
      scopeType: '2',
      regionId: '11111111-1111-1111-1111-111111111111',
    })
    expect(result.success).toBe(true)
  })

  it('requires a facilityId for Facility scope and rejects a unit id', () => {
    const missingFacility = createNoteSchema.safeParse({ ...validCreateBase, scopeType: '3' })
    expect(missingFacility.success).toBe(false)

    const withUnit = createNoteSchema.safeParse({
      ...validCreateBase,
      scopeType: '3',
      facilityId: '22222222-2222-2222-2222-222222222222',
      facilityUnitId: '33333333-3333-3333-3333-333333333333',
    })
    expect(withUnit.success).toBe(false)
  })

  it('requires both facilityId and facilityUnitId for FacilityUnit scope', () => {
    const result = createNoteSchema.safeParse({
      ...validCreateBase,
      scopeType: '4',
      facilityId: '22222222-2222-2222-2222-222222222222',
      facilityUnitId: '33333333-3333-3333-3333-333333333333',
    })
    expect(result.success).toBe(true)
  })

  it('rejects a malformed guid', () => {
    const result = createNoteSchema.safeParse({ ...validCreateBase, scopeType: '2', regionId: 'not-a-guid' })
    expect(result.success).toBe(false)
  })

  it('rejects a due date in the past', () => {
    const result = createNoteSchema.safeParse({
      ...validCreateBase,
      scopeType: '0',
      dueAtUtc: '2000-01-01T00:00',
    })
    expect(result.success).toBe(false)
  })
})

describe('updateNoteSchema', () => {
  it('does not include scope fields', () => {
    const result = updateNoteSchema.safeParse(validCreateBase)
    expect(result.success).toBe(true)
  })

  it('rejects an empty description', () => {
    const result = updateNoteSchema.safeParse({ ...validCreateBase, description: '' })
    expect(result.success).toBe(false)
  })
})

describe('assignNoteSchema', () => {
  it('requires exactly one of assignedToUserId or assignedToDepartmentId', () => {
    const neither = assignNoteSchema.safeParse({ reason: 'تكليف' })
    expect(neither.success).toBe(false)

    const both = assignNoteSchema.safeParse({
      reason: 'تكليف',
      assignedToUserId: '11111111-1111-1111-1111-111111111111',
      assignedToDepartmentId: '22222222-2222-2222-2222-222222222222',
    })
    expect(both.success).toBe(false)

    const user = assignNoteSchema.safeParse({
      reason: 'تكليف',
      assignedToUserId: '11111111-1111-1111-1111-111111111111',
    })
    expect(user.success).toBe(true)
  })

  it('requires a non-empty reason', () => {
    const result = assignNoteSchema.safeParse({
      reason: '  ',
      assignedToUserId: '11111111-1111-1111-1111-111111111111',
    })
    expect(result.success).toBe(false)
  })
})
