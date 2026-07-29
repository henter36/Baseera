import { renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { useUnsavedChangesGuard } from './useUnsavedChangesGuard'

function dispatchBeforeUnload(): Event {
  const event = new Event('beforeunload', { cancelable: true })
  window.dispatchEvent(event)
  return event
}

describe('useUnsavedChangesGuard', () => {
  it('does nothing when clean', () => {
    renderHook(() => useUnsavedChangesGuard(false))
    expect(dispatchBeforeUnload().defaultPrevented).toBe(false)
  })

  it('prevents unload when dirty', () => {
    renderHook(() => useUnsavedChangesGuard(true))
    expect(dispatchBeforeUnload().defaultPrevented).toBe(true)
  })

  it('removes the listener on cleanup, so unload is no longer blocked after unmount', () => {
    const { unmount } = renderHook(() => useUnsavedChangesGuard(true))
    unmount()
    expect(dispatchBeforeUnload().defaultPrevented).toBe(false)
  })

  it('updates behavior when the dirty flag changes across renders', () => {
    const { rerender } = renderHook(({ dirty }) => useUnsavedChangesGuard(dirty), { initialProps: { dirty: false } })
    expect(dispatchBeforeUnload().defaultPrevented).toBe(false)

    rerender({ dirty: true })
    expect(dispatchBeforeUnload().defaultPrevented).toBe(true)

    rerender({ dirty: false })
    expect(dispatchBeforeUnload().defaultPrevented).toBe(false)
  })
})
