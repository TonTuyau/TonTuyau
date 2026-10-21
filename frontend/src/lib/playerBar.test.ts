import type { Chapter } from './api/types'
import { barSegments, filledPart } from './playerBar'

const chapter = (startSeconds: number, endSeconds: number, title: string): Chapter => ({
  startSeconds,
  endSeconds,
  title,
})

describe('the player bar', () => {
  it('lays the chapters end to end over the video', () => {
    expect(barSegments([chapter(0, 60, 'Intro'), chapter(60, 180, 'Recipe')], 180)).toEqual([
      { start: 0, end: 60, title: 'Intro' },
      { start: 60, end: 180, title: 'Recipe' },
    ])
  })

  it('without chapters, is one segment', () => {
    expect(barSegments([], 95)).toEqual([{ start: 0, end: 95, title: '' }])
  })

  it('fills what no chapter covers with untitled segments, before the first, between two and after the last', () => {
    expect(barSegments([chapter(10, 50, 'A'), chapter(70, 90, 'B')], 120)).toEqual([
      { start: 0, end: 10, title: '' },
      { start: 10, end: 50, title: 'A' },
      { start: 50, end: 70, title: '' },
      { start: 70, end: 90, title: 'B' },
      { start: 90, end: 120, title: '' },
    ])
  })

  it('cuts the chapters at the end of the file, and drops those past it', () => {
    // The API's chapters came from YouTube; the file can be shorter (a stream cut, a trimmed download).
    expect(barSegments([chapter(0, 60, 'A'), chapter(60, 200, 'B'), chapter(200, 300, 'C')], 100)).toEqual([
      { start: 0, end: 60, title: 'A' },
      { start: 60, end: 100, title: 'B' },
    ])
  })

  it("joins a sliver after a chapter to it: the file is rarely the chapters' length to the second", () => {
    expect(barSegments([chapter(0, 90, 'A'), chapter(90.4, 180, 'B')], 180.05)).toEqual([
      { start: 0, end: 90, title: 'A' },
      { start: 90, end: 180.05, title: 'B' },
    ])
  })

  it('puts the chapters in order and never lets two overlap', () => {
    expect(barSegments([chapter(50, 100, 'B'), chapter(0, 60, 'A')], 100)).toEqual([
      { start: 0, end: 60, title: 'A' },
      { start: 60, end: 100, title: 'B' },
    ])
  })

  it('with no known length, is one empty segment', () => {
    expect(barSegments([chapter(0, 60, 'A')], 0)).toEqual([{ start: 0, end: 0, title: '' }])
    expect(barSegments([], Number.NaN)).toEqual([{ start: 0, end: 0, title: '' }])
  })

  it('fills a segment up to a time, and an empty one not at all', () => {
    const segment = { start: 60, end: 120, title: 'B' }
    expect(filledPart(30, segment)).toBe(0)
    expect(filledPart(90, segment)).toBe(0.5)
    expect(filledPart(500, segment)).toBe(1)
    expect(filledPart(10, { start: 0, end: 0, title: '' })).toBe(0)
  })
})
