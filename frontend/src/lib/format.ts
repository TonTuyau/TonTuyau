// Display formatting only (Intl): numbers, dates, durations, sizes. Never computes a fact the API didn't give.
import type { Lang } from './i18n/types'

/** "74K" / "74 k", "1.2M" / "1,2 M". */
export function formatCompact(value: number, lang: Lang): string {
  return new Intl.NumberFormat(lang, { notation: 'compact', maximumFractionDigits: 1 }).format(value)
}

/** "74,198" / "74 198". */
export function formatNumber(value: number, lang: Lang): string {
  return new Intl.NumberFormat(lang).format(value)
}

/** "0:15", "31:54", "1:02:03". */
export function formatDuration(totalSeconds: number): string {
  const seconds = Math.max(0, Math.floor(totalSeconds))
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = seconds % 60
  const ss = String(s).padStart(2, '0')
  return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${ss}` : `${m}:${ss}`
}

/**
 * A long play time, to the minute: "12,345 h" (100 hours and more), "3 h 05 min", "12 min", or "38 s" under a minute.
 * The units read the same in English and French.
 */
export function formatPlayTime(totalSeconds: number, lang: Lang): string {
  const seconds = Math.max(0, Math.round(totalSeconds))
  if (seconds < 60) {
    return `${seconds} s`
  }
  const minutes = Math.round(seconds / 60)
  const hours = Math.floor(minutes / 60)
  if (hours >= 100) {
    return `${formatNumber(Math.round(seconds / 3600), lang)} h`
  }
  const rest = minutes % 60
  return hours > 0 ? `${hours} h ${String(rest).padStart(2, '0')} min` : `${minutes} min`
}

const DAY = 86_400

/** "3 hours ago" / "il y a 3 heures", like YouTube: the largest whole unit. */
export function formatAge(iso: string, lang: Lang, now: Date = new Date()): string {
  const elapsed = Math.max(0, Math.floor((now.getTime() - new Date(iso).getTime()) / 1000))
  const rtf = new Intl.RelativeTimeFormat(lang, { numeric: 'always' })
  const steps: [number, Intl.RelativeTimeFormatUnit][] = [
    [365 * DAY, 'year'],
    [30 * DAY, 'month'],
    [7 * DAY, 'week'],
    [DAY, 'day'],
    [3600, 'hour'],
    [60, 'minute'],
  ]
  for (const [size, unit] of steps) {
    if (elapsed >= size) {
      return rtf.format(-Math.floor(elapsed / size), unit)
    }
  }
  return rtf.format(-elapsed, 'second')
}

/** "Apr 24, 2026" / "24 avr. 2026". */
export function formatDate(iso: string, lang: Lang): string {
  return new Intl.DateTimeFormat(lang, { dateStyle: 'medium' }).format(new Date(iso))
}

/** "1.3 GB" / "1,3 Go". */
export function formatBytes(bytes: number, lang: Lang): string {
  const units = ['byte', 'kilobyte', 'megabyte', 'gigabyte', 'terabyte'] as const
  let value = bytes
  let index = 0
  while (value >= 1000 && index < units.length - 1) {
    value /= 1000
    index++
  }
  return new Intl.NumberFormat(lang, {
    style: 'unit',
    unit: units[index],
    unitDisplay: 'short',
    maximumFractionDigits: 1,
  }).format(value)
}

/** "5.4 Mb/s". */
export function formatBitrate(bitsPerSecond: number, lang: Lang): string {
  const kilo = bitsPerSecond >= 1_000_000 ? bitsPerSecond / 1_000_000 : bitsPerSecond / 1000
  const unit = bitsPerSecond >= 1_000_000 ? 'megabit-per-second' : 'kilobit-per-second'
  return new Intl.NumberFormat(lang, { style: 'unit', unit, unitDisplay: 'short', maximumFractionDigits: 1 }).format(
    kilo,
  )
}

const codecNames: Record<string, string> = {
  av1: 'AV1',
  vp9: 'VP9',
  vp8: 'VP8',
  h264: 'H.264',
  hevc: 'HEVC (H.265)',
  h265: 'HEVC (H.265)',
  vvc: 'VVC (H.266)',
  opus: 'Opus',
  aac: 'AAC',
  mp3: 'MP3',
  vorbis: 'Vorbis',
  flac: 'FLAC',
  // Seen in the real library (7 files): Dolby audio in an MP4.
  eac3: 'E-AC-3 (Dolby Digital Plus)',
  ac3: 'AC-3 (Dolby Digital)',
}

/** Display name of a codec family as the API names it ("av1" → "AV1"); unknown names shown as given. */
export function codecName(codec: string): string {
  return codecNames[codec.toLowerCase()] ?? codec
}
