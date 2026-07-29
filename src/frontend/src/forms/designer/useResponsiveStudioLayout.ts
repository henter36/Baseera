import { useEffect, useState } from 'react'

export type StudioLayoutMode = 'desktop' | 'tablet' | 'mobile'

const TABLET_MAX = 1180
const MOBILE_MAX = 768

function resolveMode(width: number): StudioLayoutMode {
  if (width <= MOBILE_MAX) return 'mobile'
  if (width <= TABLET_MAX) return 'tablet'
  return 'desktop'
}

export function useResponsiveStudioLayout(): StudioLayoutMode {
  const [mode, setMode] = useState<StudioLayoutMode>(() =>
    typeof window === 'undefined' ? 'desktop' : resolveMode(window.innerWidth),
  )

  useEffect(() => {
    const update = () => setMode(resolveMode(window.innerWidth))
    update()
    window.addEventListener('resize', update)
    return () => window.removeEventListener('resize', update)
  }, [])

  return mode
}
