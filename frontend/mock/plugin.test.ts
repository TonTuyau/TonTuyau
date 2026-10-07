// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { loadDb } from './db.ts'
import { missingOnShare, sampleFor } from './plugin.ts'

const db = loadDb()
const video = (id: string) => {
  const found = db.videos.find((candidate) => candidate.id === id)
  if (!found) throw new Error(`no fixture ${id}`)
  return found
}

describe('mock media (player shape edge cases, M5)', () => {
  it("serves a test pattern with the video track's shape", () => {
    expect(sampleFor(video('mock4by3000'))).toBe('fourByThree')
    expect(sampleFor(video('mockVertic0'))).toBe('portrait')
    expect(sampleFor(video('mockWide000'))).toBe('wide')
    expect(sampleFor(video('mockFresh01'))).toBe('landscape')
    expect(sampleFor(video('mockShort01'))).toBe('portrait')
  })

  it('serves the 4:3 pattern when the API has no video track, so the shape read from the file shows', () => {
    expect(video('mockNoVTrk0').tracks.some((track) => track.kind === 'video')).toBe(false)
    expect(sampleFor(video('mockNoVTrk0'))).toBe('fourByThree')
  })

  it('has a file the API says is there but the share does not have', () => {
    expect(video('mockGoneFil').mediaAvailable).toBe(true)
    expect(missingOnShare('mockGoneFil')).toBe(true)
    expect(missingOnShare('mockFresh01')).toBe(false)
  })
})
