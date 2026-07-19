type MsalInitializable = {
  initialize: () => Promise<void>
}

let msalInitializationPromise: Promise<void> | null = null

/** Test-only: clears the shared initialize promise between cases. */
export function resetMsalInitializationForTests(): void {
  msalInitializationPromise = null
}

/**
 * Single-flight MSAL initialize(): concurrent callers share one Promise.
 */
export function ensureMsalInitialized(
  instance: MsalInitializable | null,
  initError: string | null,
): Promise<void> {
  if (!instance) {
    return Promise.reject(new Error(initError || 'Entra غير مهيأ'))
  }

  msalInitializationPromise ??= instance.initialize()
  return msalInitializationPromise
}
