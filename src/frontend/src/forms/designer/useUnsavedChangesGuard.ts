import { useEffect } from 'react'

/**
 * Blocks tab close / refresh / external navigation while there are unsaved changes.
 *
 * The app renders inside a plain `<BrowserRouter>` (declarative mode), not a data router, so
 * react-router's `useBlocker`/`unstable_usePrompt` (which require a data router) cannot be used
 * to intercept in-app `<Link>`/`navigate()` calls here. Migrating the whole app to a data router
 * is out of scope for this phase (see phase2a-form-designer-scope.md). This hook therefore covers
 * the browser-level exit paths (close tab, refresh, back/forward to another origin, typed URL);
 * in-app navigation instead calls `flush()` before leaving so autosave has a final chance to
 * persist the latest schema.
 */
export function useUnsavedChangesGuard(isDirty: boolean) {
  useEffect(() => {
    if (!isDirty) {
      return
    }

    // Calling preventDefault() is the modern (spec-compliant) way to trigger the browser's
    // generic "leave site?" prompt. `event.returnValue` is a legacy fallback some very old
    // browser versions required; the project has no documented legacy-browser target
    // (no browserslist/legacy build config), so it is not set here.
    const handler = (event: BeforeUnloadEvent) => {
      event.preventDefault()
    }

    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [isDirty])
}
