import type { Track } from './api/types'
import { ratioFromElement, ratioFromTracks, ratioCss } from './videoRatio'

const video = (width: number | null, height: number | null): Track => ({
  kind: 'video',
  codec: 'av1',
  width,
  height,
  bitrate: 1,
})
const audio: Track = { kind: 'audio', codec: 'opus', width: null, height: null, bitrate: 1 }

describe('videoRatio', () => {
  it("reads the first video track's size, as the API gives it", () => {
    expect(ratioFromTracks([audio, video(1080, 1920)])).toEqual({ width: 1080, height: 1920 })
    expect(ratioFromTracks([video(640, 480), video(1920, 1080)])).toEqual({ width: 640, height: 480 })
  })

  it('has no ratio without a sized video track (audio only, or a track without size)', () => {
    expect(ratioFromTracks([audio])).toBeNull()
    expect(ratioFromTracks([])).toBeNull()
    expect(ratioFromTracks([video(null, null)])).toBeNull()
    expect(ratioFromTracks([video(0, 720)])).toBeNull()
  })

  it("reads the loaded file's size from the element, none for an audio-only file", () => {
    expect(ratioFromElement({ videoWidth: 480, videoHeight: 360 })).toEqual({ width: 480, height: 360 })
    expect(ratioFromElement({ videoWidth: 0, videoHeight: 0 })).toBeNull()
  })

  it('gives the box its aspect ratio and the number for its width cap', () => {
    expect(ratioCss({ width: 1080, height: 1920 })).toEqual({ ratio: '1080 / 1920', value: 0.5625 })
  })
})
