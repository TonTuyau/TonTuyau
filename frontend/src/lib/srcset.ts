// Resized art (QA09, contract v1.4): the API lists each image's versions ({ width, url }, smallest first, the
// original last); the front only joins them into `srcset` and says how wide the image is shown (`sizes`), so the
// browser picks the smallest version that is sharp enough. No URL is built here.
import type { ImageSize } from './api/types'

/** `srcset` from the API's list ("url 360w, url 720w, …"), or undefined when the list is empty. */
export function srcset(sizes: readonly ImageSize[] | undefined): string | undefined {
  return sizes && sizes.length > 0 ? sizes.map((size) => `${size.url} ${size.width}w`).join(', ') : undefined
}

/**
 * The interface's scale on big screens (2.1.0), as theme/youtube.css sets it with the root's font size: its media
 * conditions and factors, biggest first. Every image is shown that much wider there, so `sizes` follows.
 */
export const uiScales = [
  { when: '(min-width: 4800px) and (min-height: 2600px)', factor: 2 },
  { when: '(min-width: 3200px) and (min-height: 1800px)', factor: 1.5 },
  { when: '(min-width: 2400px) and (min-height: 1250px)', factor: 1.25 },
] as const

/** A width in CSS px at the normal scale, then the same scaled on big screens: "(…) 336px, …, 168px". */
function scaled(px: number): string {
  return [...uiScales.map(({ when, factor }) => `${when} ${px * factor}px`), `${px}px`].join(', ')
}

/**
 * A grid's cards on big screens: at least the normal card's widest, scaled; past the column cap, (viewport − the
 * guide, the padding and the gaps, scaled) / columns.
 */
function scaledGrid(card: number, minus: number, perColumn: string): string {
  return uiScales
    .map(({ when, factor }) => `${when} max(${card * factor}px, calc((100vw - ${minus * factor}px)${perColumn}))`)
    .join(', ')
}

/**
 * How wide each layout shows its images, for the `sizes` attribute (CSS custom properties can't be read there, so
 * these follow the theme's tokens by hand: --card-min-width, --grid-max-columns, --compact-thumb-width,
 * --row-thumb-width, --short-card-width, --shorts-max-columns, the avatar sizes, --page-max-width; 600px is the phone
 * breakpoint), on big screens scaled as the theme scales (uiScales). srcset.test.ts recomputes them from the theme.
 */
export const imageSizes = {
  /**
   * Home, feeds and channel grids: 300–400 px cards, one column on phones. On very wide screens the grid stops at 6
   * columns and the cards grow: (viewport − 200 px) / 6, where 200 px = the mini guide (72) + the page's padding (2 ×
   * 24) + 5 gaps (5 × 16), an upper bound (the full guide leaves less). That passes 400 px from a 2600 px viewport on.
   */
  grid: `(max-width: 600px) 100vw, ${scaledGrid(400, 200, ' / 6')}, (min-width: 2600px) calc((100vw - 200px) / 6), 400px`,
  /** The watch page's side column. */
  compact: scaled(168),
  /** Search results: the thumbnail on the left, full width on phones. */
  row: `(max-width: 600px) 100vw, ${scaled(500)}`,
  /**
   * Shorts cards: 200 px wide, two per row on phones. Short thumbnails are 16:9 frames with the vertical picture in
   * the middle, and the portrait card crops that strip (object-fit: cover): the image is drawn 16/9 ÷ 9/16 ≈ 3.16×
   * the card's width (633 px for a 200 px card), and that drawn width is what the browser must pick a version for.
   * On very wide screens the shorts grid stops at 10 columns and the cards grow: (viewport − 192 px) / 10, where
   * 192 px = the mini guide (72) + the page's padding (48) + 9 gaps (9 × 8); drawn 256/81 of that. That passes 200 px
   * (633 drawn) from a 2192 px viewport on.
   */
  short: `(max-width: 600px) 158vw, ${scaledGrid(633, 192, ' * 256 / 810')}, (min-width: 2192px) calc((100vw - 192px) * 256 / 810), 633px`,
  avatar: { xs: scaled(24), sm: scaled(36), md: scaled(88), lg: scaled(160) },
  /** The channel banner: the page width, at most 1284 px (scaled on big screens). */
  banner: `(max-width: 1284px) 100vw, ${uiScales.map(({ when, factor }) => `${when} ${1284 * factor}px`).join(', ')}, 1284px`,
} as const
