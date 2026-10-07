// Descriptions (QA06, M5a-7): http(s) URLs become links. A display concern only: the text is split into
// plain-text and link segments that the template renders as text nodes and <a> elements (never as HTML).
// Timestamps ("12:30"), bare domains and other schemes stay plain text.

export type Segment = { kind: 'text'; text: string } | { kind: 'link'; text: string; href: string }

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

export function linkify(text: string): Segment[] {
  const segments: Segment[] = []
  const pushText = (value: string) => {
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
