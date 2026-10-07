// Resized art in the mock (contract v1.4): the version lists the backend would give for an image, from its URL.
// The mock's own art is drawn on request (SVG) at any width, so every listed version exists.
import type { ImageSize } from '../src/lib/api/types.ts'

export const videoWidths = [360, 720]
export const channelThumbWidths = [48, 96, 176, 320]
export const bannerWidths = [1280]

/** The versions of an image: the resized ones narrower than the original, then the original itself (or none). */
export function versionsOf(url: string | null, widths: readonly number[], originalWidth: number): ImageSize[] {
  if (url === null) return []
  return [
    ...widths.filter((width) => width < originalWidth).map((width) => ({ width, url: `${url}/${width}` })),
    { width: originalWidth, url },
  ]
}

/** A width segment the backend serves for this kind of image (anything else is a 404). */
export function servedWidth(kind: 'video' | 'thumb' | 'banner', segment: string): number | null {
  const widths = kind === 'video' ? videoWidths : kind === 'thumb' ? channelThumbWidths : bannerWidths
  return widths.find((width) => String(width) === segment) ?? null
}
