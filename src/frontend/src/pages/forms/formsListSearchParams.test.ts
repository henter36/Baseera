import { describe, expect, it } from 'vitest'
import { buildFormsListSearchParams } from './formsListSearchParams'

describe('buildFormsListSearchParams', () => {
  it('returns an empty URL for default filter values', () => {
    const params = buildFormsListSearchParams({
      page: 1,
      pageSize: 20,
      sortBy: 'createdAtUtc',
      sortDesc: true,
    })
    expect(params.toString()).toBe('')
  })

  it('keeps ascending sort as sortDesc=false', () => {
    const params = buildFormsListSearchParams({
      page: 1,
      pageSize: 20,
      sortBy: 'nameAr',
      sortDesc: false,
    })
    expect(params.get('sortBy')).toBe('nameAr')
    expect(params.get('sortDesc')).toBe('false')
  })

  it('serializes scope and search filters', () => {
    const params = buildFormsListSearchParams({
      page: 1,
      pageSize: 20,
      sortBy: 'createdAtUtc',
      sortDesc: true,
      search: 'FORM-1',
      status: 0,
      classification: 2,
      regionId: 'region-1',
      facilityId: 'facility-1',
    })
    expect(params.get('search')).toBe('FORM-1')
    expect(params.get('status')).toBe('0')
    expect(params.get('classification')).toBe('2')
    expect(params.get('regionId')).toBe('region-1')
    expect(params.get('facilityId')).toBe('facility-1')
  })

  it('includes page when greater than 1', () => {
    const params = buildFormsListSearchParams({
      page: 2,
      pageSize: 20,
      sortBy: 'createdAtUtc',
      sortDesc: true,
    })
    expect(params.get('page')).toBe('2')
  })
})
