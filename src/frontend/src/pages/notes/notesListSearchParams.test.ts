import { describe, expect, it } from 'vitest'
import { buildNotesListSearchParams } from './notesListSearchParams'

describe('buildNotesListSearchParams', () => {
  it('returns an empty URL for default filter values', () => {
    const params = buildNotesListSearchParams({
      page: 1,
      pageSize: 20,
      sortBy: 'createdAtUtc',
      sortDesc: true,
    })
    expect(params.toString()).toBe('')
  })

  it('keeps ascending sort as sortDesc=false', () => {
    const params = buildNotesListSearchParams({
      page: 1,
      pageSize: 20,
      sortBy: 'dueAtUtc',
      sortDesc: false,
    })
    expect(params.get('sortBy')).toBe('dueAtUtc')
    expect(params.get('sortDesc')).toBe('false')
  })

  it('serializes boolean filters', () => {
    const params = buildNotesListSearchParams({
      page: 1,
      pageSize: 20,
      sortBy: 'createdAtUtc',
      sortDesc: true,
      overdueOnly: true,
      unassignedOnly: true,
      requiresMyAction: true,
      requiresRouting: true,
    })
    expect(params.get('overdueOnly')).toBe('true')
    expect(params.get('unassignedOnly')).toBe('true')
    expect(params.get('requiresMyAction')).toBe('true')
    expect(params.get('requiresRouting')).toBe('true')
  })

  it('serializes scope and search filters', () => {
    const params = buildNotesListSearchParams({
      page: 1,
      pageSize: 20,
      sortBy: 'createdAtUtc',
      sortDesc: true,
      search: 'OBS-1',
      status: 1,
      severity: 2,
      noteTypeId: 'type-1',
      classification: 3,
      regionId: 'region-1',
      facilityId: 'facility-1',
      facilityUnitId: 'unit-1',
      ownerDepartmentId: 'dept-1',
      dueSoonDays: 7,
    })
    expect(params.get('search')).toBe('OBS-1')
    expect(params.get('status')).toBe('1')
    expect(params.get('severity')).toBe('2')
    expect(params.get('noteTypeId')).toBe('type-1')
    expect(params.get('classification')).toBe('3')
    expect(params.get('regionId')).toBe('region-1')
    expect(params.get('facilityId')).toBe('facility-1')
    expect(params.get('facilityUnitId')).toBe('unit-1')
    expect(params.get('ownerDepartmentId')).toBe('dept-1')
    expect(params.get('dueSoonDays')).toBe('7')
  })

  it('includes page when greater than 1', () => {
    const params = buildNotesListSearchParams({
      page: 3,
      pageSize: 20,
      sortBy: 'createdAtUtc',
      sortDesc: true,
    })
    expect(params.get('page')).toBe('3')
  })
})
