// The player's bar (2.0, C2): the chapters as segments laid end to end over the whole video, as YouTube does.
import type { Chapter } from './api/types'

export interface Segment {
  start: number
  end: number
  /** The chapter's; '' for a stretch no chapter covers (before the first, between two). */
  title: string
}

/**
 * Less than this between two chapters, or after the last one, is no stretch of its own: the file's length is rarely
 * the chapters' to the second (a file of 180.05 s for chapters ending at 180).
 */
export const SLIVER_SECONDS = 1

/**
 * The bar's segments, from 0 to `total`: the chapters in order, cut at the video's end and at the next one's start
 * (YouTube's never overlap, but nothing promises it), the stretches no chapter covers as untitled segments (slivers
 * joined to the chapter before). No chapter: one segment. No known length: one empty segment.
 */
export function barSegments(chapters: readonly Chapter[], total: number): Segment[] {
  if (!(total > 0)) {
    return [{ start: 0, end: 0, title: '' }]
  }
  const segments: Segment[] = []
  let at = 0
  for (const chapter of [...chapters].sort((a, b) => a.startSeconds - b.startSeconds)) {
    const start = Math.max(at, chapter.startSeconds, 0)
    const end = Math.min(chapter.endSeconds, total)
    if (end <= start) {
      continue
    }
    if (start - at >= SLIVER_SECONDS) {
      segments.push({ start: at, end: start, title: '' })
    }
    segments.push({ start: start - at >= SLIVER_SECONDS ? start : at, end, title: chapter.title })
    at = end
  }
  const last = segments.at(-1)
  if (last && total - at < SLIVER_SECONDS) {
    last.end = total
  } else if (at < total) {
    segments.push({ start: at, end: total, title: '' })
  }
  return segments
}

/** How much of a segment is before `seconds`, from 0 to 1 (0 for an empty one). */
export function filledPart(seconds: number, segment: Segment): number {
  if (segment.end <= segment.start) {
    return 0
  }
  return (Math.min(Math.max(seconds, segment.start), segment.end) - segment.start) / (segment.end - segment.start)
}
