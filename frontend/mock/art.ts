// Placeholder images for /art/*, generated on the fly: an SVG gradient with initials. Never real artwork.
import type { ImageSize } from '../src/lib/api/types.ts'
import { servedWidth } from './imageSizes.ts'
import type { MockDb } from './db.ts'

function hash(value: string): number {
  let h = 2166136261
  for (const char of value) {
    h = Math.imul(h ^ (char.codePointAt(0) ?? 0), 16777619)
  }
  return h >>> 0
}

function escapeXml(value: string): string {
  return value.replace(/[<>&"']/g, (char) => `&#${char.codePointAt(0)};`)
}

function initials(name: string): string {
  const letters = name
    .split(/[^\p{L}\p{N}]+/u)
    .filter((word) => word.length > 2 || /\p{N}/u.test(word))
    .slice(0, 2)
    .map((word) => [...word][0] ?? '')
  return (letters.join('') || [...name][0] || '?').toUpperCase()
}

function svg(width: number, height: number, seed: string, big: string, small: string): string {
  const h = hash(seed)
  const hue = h % 360
  const hue2 = (hue + 40 + (h % 80)) % 360
  const angle = h % 2 === 0 ? 'x2="1" y2="1"' : 'x2="1" y2="0"'
  const bigSize = Math.round(Math.min(width, height) * 0.38)
  const smallSize = Math.round(Math.min(width, height) * 0.07)
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">
<defs><linearGradient id="g" x1="0" y1="0" ${angle}><stop offset="0" stop-color="hsl(${hue} 65% 45%)"/><stop offset="1" stop-color="hsl(${hue2} 70% 25%)"/></linearGradient></defs>
<rect width="100%" height="100%" fill="url(#g)"/>
<text x="50%" y="50%" fill="white" fill-opacity="0.92" font-family="system-ui, sans-serif" font-weight="700" font-size="${bigSize}" text-anchor="middle" dominant-baseline="central">${escapeXml(big)}</text>
${small ? `<text x="50%" y="${height - smallSize * 1.4}" fill="white" fill-opacity="0.75" font-family="system-ui, sans-serif" font-size="${smallSize}" text-anchor="middle">${escapeXml(small)}</text>` : ''}
</svg>`
}

/** The SVG for an /art path, or null (→ 404) when the item has no such image. */
export function artFor(db: MockDb, path: string): string | null {
  const [, , scope, id, second, third, ...rest] = path.split('/').map((part) => decodeURIComponent(part))
  if (rest.length > 0) return null
  if (scope === 'videos' && id && third === undefined) {
    const video = db.videos.find((candidate) => candidate.id === id)
    // Ids starting with "mockArt404" announce a thumbnail the server can't find (edge case).
    if (!video || !video.thumbnailUrl || id.startsWith('mockArt404')) return null
    // A resized version (v1.4, /art/videos/<id>/<width>): only the widths the API lists for this video.
    const width = second === undefined ? null : listed(video.thumbnails, path, servedWidth('video', second))
    if (second !== undefined && width === null) return null
    const channel = db.channels.find((candidate) => candidate.id === video.channelId)
    const [w, h] = video.type === 'short' ? [720, 1280] : [1280, 720]
    return svg(width ?? w, Math.round(((width ?? w) * h) / w), id, initials(video.title), channel?.name ?? '')
  }
  if (scope === 'channels' && id && (second === 'thumb' || second === 'banner')) {
    const channel = db.channels.find((candidate) => candidate.id === id)
    if (!channel) return null
    const list = second === 'thumb' ? channel.thumbnails : channel.banners
    const width = third === undefined ? null : listed(list, path, servedWidth(second, third))
    if (third !== undefined && width === null) return null
    if (second === 'thumb' && channel.thumbnailUrl) {
      return svg(width ?? 512, width ?? 512, id, initials(channel.name), '')
    }
    if (second === 'banner' && channel.bannerUrl) {
      return svg(width ?? 2560, Math.round(((width ?? 2560) * 424) / 2560), `${id}-banner`, channel.name, '')
    }
  }
  return null
}

/** The width, if it's one the backend makes and the image's version list (as the API gives it) has this URL. */
function listed(versions: readonly ImageSize[], path: string, width: number | null): number | null {
  return width !== null && versions.some((version) => version.url === path) ? width : null
}
