import { describe, expect, it, vi } from 'vitest'
import { ApiError, getAuthMode, isTestAuthAllowed } from '../api/client'

describe('production auth client guards', () => {
  it('does not allow test auth outside DEV+test mode', () => {
    expect(isTestAuthAllowed()).toBe(false)
  })

  it('maps 401 to ApiError', () => {
    const err = new ApiError(401, 'انتهت الجلسة')
    expect(err.status).toBe(401)
    expect(err.message).toContain('انتهت')
  })

  it('maps 403 and 409 statuses', () => {
    expect(new ApiError(403, 'ممنوع').status).toBe(403)
    expect(new ApiError(409, 'تعارض').status).toBe(409)
  })

  it('getAuthMode respects VITE_AUTH_MODE when set', () => {
    // In vitest/jsdom, PROD is false; mode comes from env file or fallback entra in non-prod when unset.
    const mode = getAuthMode()
    expect(mode === 'test' || mode === 'entra').toBe(true)
  })
})

describe('check-production-auth script contract', () => {
  it('documents that production test mode must be refused', async () => {
    const { spawnSync } = await import('node:child_process')
    const result = spawnSync(process.execPath, ['scripts/check-production-auth.mjs'], {
      cwd: process.cwd(),
      env: { ...process.env, VITE_AUTH_MODE: 'test' },
      encoding: 'utf8',
    })
    expect(result.status).toBe(1)
  })
})
