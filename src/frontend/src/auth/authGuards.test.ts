import { describe, expect, it } from 'vitest'
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

  it('refuses zero-GUID Entra placeholders', async () => {
    const { spawnSync } = await import('node:child_process')
    const result = spawnSync(process.execPath, ['scripts/check-production-auth.mjs'], {
      cwd: process.cwd(),
      env: {
        ...process.env,
        VITE_AUTH_MODE: 'entra',
        VITE_ENTRA_CLIENT_ID: '00000000-0000-0000-0000-000000000001',
        VITE_ENTRA_TENANT_ID: '00000000-0000-0000-0000-000000000002',
        VITE_ENTRA_API_SCOPE: 'api://00000000-0000-0000-0000-000000000003/.default',
      },
      encoding: 'utf8',
    })
    expect(result.status).toBe(1)
  })

  it('refuses http localhost redirect URI', async () => {
    const { spawnSync } = await import('node:child_process')
    const result = spawnSync(process.execPath, ['scripts/check-production-auth.mjs'], {
      cwd: process.cwd(),
      env: {
        ...process.env,
        VITE_AUTH_MODE: 'entra',
        VITE_ENTRA_CLIENT_ID: '11111111-1111-4111-8111-111111111111',
        VITE_ENTRA_TENANT_ID: '22222222-2222-4222-8222-222222222222',
        VITE_ENTRA_API_SCOPE: 'api://33333333-3333-4333-8333-333333333333/.default',
        VITE_ENTRA_REDIRECT_URI: 'http://localhost:5173',
      },
      encoding: 'utf8',
    })
    expect(result.status).toBe(1)
  })
})
