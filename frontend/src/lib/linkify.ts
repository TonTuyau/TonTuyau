// Descriptions (QA06, M5a-7): http(s) URLs become links. A display concern only: the text is split into
// plain-text and link segments that the template renders as text nodes and <a> elements (never as HTML).
// Bare domains and other schemes stay plain text. Timecodes ("1:23", "1:02:03") become `time` segments only when
// the caller gives the video's duration (1.1.0, the watch page's description): YouTube's rules, see parseTimecode().

export type Segment =
  | { kind: 'text'; text: string }
  | { kind: 'link'; text: string; href: string }
  | { kind: 'time'; text: string; seconds: number }

export interface LinkifyOptions {
  /** The video's duration from the API (seconds): timecodes up to it become `time` segments. */
  duration?: number
}

/**
 * A timecode as YouTube links it: m:ss, mm:ss, h:mm:ss or hh:mm:ss, seconds 00–59 (two digits), and minutes 00–59
 * (two digits) when there are hours. Not inside a word or a number ("v1:23", "1:23:45:67", "1:23.5"); "3:5", "1:60"
 * and ratios such as "16:9" aren't timecodes.
 */
const timecodePattern =
  /(?<![\p{L}\p{N}_:]|\p{N}[.,])(?:(\d{1,2}):(\d{2})|(\d{1,2})):(\d{2})(?![\p{L}\p{N}_]|[:.,]\p{N})/gu

/** The seconds a timecode stands for, or null when it isn't one (the rules of timecodePattern). */
export function parseTimecode(text: string): number | null {
  const match = new RegExp(`^(?:${timecodePattern.source})$`, 'u').exec(text)
  return match ? secondsOf(match) : null
}

function secondsOf(match: RegExpExecArray | RegExpMatchArray): number | null {
  const [, hours, minutesAfterHours, minutesAlone, seconds] = match
  const minutes = Number(minutesAfterHours ?? minutesAlone)
  if (Number(seconds) > 59 || (hours !== undefined && minutes > 59)) {
    return null
  }
  return Number(hours ?? 0) * 3600 + minutes * 60 + Number(seconds)
}

/** An http(s) URL that doesn't start in the middle of a word or of another URL. */
const urlPattern = /(?<![\p{L}\p{N}_@/.-])https?:\/\/[^\s<>"`]+/giu

const trailing = new Set(['.', ',', ';', ':', '!', '?', "'", '"', '»', '…'])
const closers: Record<string, string> = { ')': '(', ']': '[', '}': '{' }

function count(text: string, char: string): number {
  return text.split(char).length - 1
}

/** Drops the punctuation that ends a sentence rather than the URL (a closing bracket only if unbalanced). */
function trimEnd(candidate: string): string {
  let url = candidate
  for (;;) {
    const last = url.at(-1) ?? ''
    const opener = closers[last]
    if (trailing.has(last) || (opener !== undefined && count(url, last) > count(url, opener))) {
      url = url.slice(0, -1)
    } else {
      return url
    }
  }
}

/** The link target, or null when the text isn't a usable http(s) URL. */
function target(url: string): string | null {
  try {
    const parsed = new URL(url)
    return (parsed.protocol === 'http:' || parsed.protocol === 'https:') && parsed.hostname !== '' ? parsed.href : null
  } catch {
    return null
  }
}

export function linkify(text: string, options: LinkifyOptions = {}): Segment[] {
  const segments: Segment[] = []
  const pushPlain = (value: string) => {
    if (value === '') {
      return
    }
    const last = segments.at(-1)
    if (last?.kind === 'text') {
      last.text += value
    } else {
      segments.push({ kind: 'text', text: value })
    }
  }
  // Text between URLs: its timecodes within the duration become `time` segments.
  const { duration } = options
  const pushText = (value: string) => {
    if (duration === undefined) {
      pushPlain(value)
      return
    }
    let from = 0
    for (const match of value.matchAll(timecodePattern)) {
      const seconds = secondsOf(match)
      if (seconds === null || seconds > duration) {
        continue
      }
      pushPlain(value.slice(from, match.index))
      segments.push({ kind: 'time', text: match[0], seconds })
      from = match.index + match[0].length
    }
    pushPlain(value.slice(from))
  }
  let position = 0
  for (const match of text.matchAll(urlPattern)) {
    const url = trimEnd(match[0])
    const href = target(url)
    if (href === null) {
      continue
    }
    pushText(text.slice(position, match.index))
    segments.push({ kind: 'link', text: url, href })
    position = match.index + url.length
  }
  pushText(text.slice(position))
  return segments
}
