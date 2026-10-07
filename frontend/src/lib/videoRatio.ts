// The shape of the player box, so a vertical, square, 4:3 or cinema-wide video fills it with no black bars.
// The ratio is read from the API (the video track's width and height in VideoDetail.tracks), never guessed; when
// the API has no video track, the player uses what the browser reports once the file's metadata has loaded.
import type { Track } from './api/types'

export interface Ratio {
  width: number
  height: number
}

/** The first video track's size, as the API gives it, or null when there's none (or it has no size). */
export function ratioFromTracks(tracks: readonly Track[]): Ratio | null {
  const video = tracks.find((track) => track.kind === 'video')
  return video?.width && video.height && video.width > 0 && video.height > 0
    ? { width: video.width, height: video.height }
    : null
}

/** What the browser reports for the loaded file (`videoWidth` / `videoHeight`), or null for an audio-only file. */
export function ratioFromElement(element: Pick<HTMLVideoElement, 'videoWidth' | 'videoHeight'>): Ratio | null {
  return element.videoWidth > 0 && element.videoHeight > 0
    ? { width: element.videoWidth, height: element.videoHeight }
    : null
}

/** The values of the player box's custom properties: `--video-ratio` (aspect-ratio) and `--video-ratio-value`. */
export function ratioCss(ratio: Ratio): { ratio: string; value: number } {
  return { ratio: `${ratio.width} / ${ratio.height}`, value: ratio.width / ratio.height }
}
