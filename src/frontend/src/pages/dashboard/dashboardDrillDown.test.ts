import { describe, expect, it } from 'vitest'
import { buildCorrectiveActionsDrillDown, buildNotesDrillDown } from './dashboardDrillDown'

describe('dashboardDrillDown', () => {
  it('builds notes drill-down with overdue and scope filters', () => {
    expect(buildNotesDrillDown({
      regionId: 'region-1',
      facilityId: 'fac-1',
      overdueOnly: true,
      sortBy: 'dueAtUtc',
      sortDesc: true,
    })).toBe('/notes?regionId=region-1&facilityId=fac-1&overdueOnly=true&sortBy=dueAtUtc&sortDesc=true')
  })

  it('preserves explicit ascending sort', () => {
    expect(buildNotesDrillDown({
      sortBy: 'dueAtUtc',
      sortDesc: false,
    })).toBe('/notes?sortBy=dueAtUtc&sortDesc=false')
  })

  it('builds corrective actions drill-down', () => {
    expect(buildCorrectiveActionsDrillDown({ overdueOnly: true })).toBe('/corrective-actions?overdueOnly=true')
  })
})
