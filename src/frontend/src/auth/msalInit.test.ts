import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ensureMsalInitialized, resetMsalInitializationForTests } from './msalInit'

describe('ensureMsalInitialized single-flight', () => {
  beforeEach(() => {
    resetMsalInitializationForTests()
  })

  it('calls initialize exactly once when invoked concurrently', async () => {
    let resolveInit!: () => void
    const initialize = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveInit = resolve
        }),
    )
    const instance = { initialize }

    const a = ensureMsalInitialized(instance, null)
    const b = ensureMsalInitialized(instance, null)
    expect(initialize).toHaveBeenCalledTimes(1)

    resolveInit()
    await Promise.all([a, b])
    expect(initialize).toHaveBeenCalledTimes(1)
  })

  it('rejects with Arabic message when instance is missing', async () => {
    await expect(ensureMsalInitialized(null, 'فشل إعداد Entra ID')).rejects.toThrow(
      'فشل إعداد Entra ID',
    )
  })

  it('rejects with default Arabic message when error is unset', async () => {
    await expect(ensureMsalInitialized(null, null)).rejects.toThrow('Entra غير مهيأ')
  })

  it('propagates initialize failure to all waiters', async () => {
    const initialize = vi.fn(() => Promise.reject(new Error('init boom')))
    const instance = { initialize }

    const a = ensureMsalInitialized(instance, null)
    const b = ensureMsalInitialized(instance, null)
    await expect(a).rejects.toThrow('init boom')
    await expect(b).rejects.toThrow('init boom')
    expect(initialize).toHaveBeenCalledTimes(1)
  })

  it('allows retry after a failed initialize', async () => {
    const initialize = vi
      .fn()
      .mockRejectedValueOnce(new Error('init boom'))
      .mockResolvedValueOnce(undefined)
    const instance = { initialize }

    await expect(ensureMsalInitialized(instance, null)).rejects.toThrow('init boom')
    await expect(ensureMsalInitialized(instance, null)).resolves.toBeUndefined()
    expect(initialize).toHaveBeenCalledTimes(2)
  })

  it('allows login waiter to share the same in-flight initialize promise', async () => {
    let resolveInit!: () => void
    const initialize = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveInit = resolve
        }),
    )
    const instance = { initialize }

    const boot = ensureMsalInitialized(instance, null)
    const loginWait = ensureMsalInitialized(instance, null)
    expect(initialize).toHaveBeenCalledTimes(1)
    resolveInit()
    await expect(boot).resolves.toBeUndefined()
    await expect(loginWait).resolves.toBeUndefined()
  })

  it('simulates StrictMode double effect without re-initialize', async () => {
    const initialize = vi.fn(async () => undefined)
    const instance = { initialize }

    await ensureMsalInitialized(instance, null)
    await ensureMsalInitialized(instance, null)
    expect(initialize).toHaveBeenCalledTimes(1)
  })
})
