import { describe, expect, it } from 'vitest'
import { getPreviewWidth, PREVIEW_WIDTHS } from './formPreviewWidths'

describe('formPreviewWidths', () => {
  it('maps modes to fixed widths', () => {
    expect(getPreviewWidth('desktop')).toBe(1024)
    expect(getPreviewWidth('tablet')).toBe(768)
    expect(getPreviewWidth('mobile')).toBe(360)
    expect(PREVIEW_WIDTHS).toEqual({
      desktop: 1024,
      tablet: 768,
      mobile: 360,
    })
  })
})
