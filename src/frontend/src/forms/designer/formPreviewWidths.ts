export const PREVIEW_WIDTHS: Record<'desktop' | 'tablet' | 'mobile', number> = {
  desktop: 1024,
  tablet: 768,
  mobile: 360,
}

export function getPreviewWidth(mode: 'desktop' | 'tablet' | 'mobile'): number {
  return PREVIEW_WIDTHS[mode]
}
