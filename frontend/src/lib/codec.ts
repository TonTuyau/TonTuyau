// R2-13: ask the browser whether it can play the file's MIME type (as the API gives it) before showing a
// player. Asking the browser about its own capabilities is not inferring anything about the data.

export interface MediaElementProbe {
  canPlayType(type: string): string
}

export interface MediaSourceProbe {
  isTypeSupported(type: string): boolean
}

function defaultVideo(): MediaElementProbe | undefined {
  return typeof document === 'undefined' ? undefined : document.createElement('video')
}

/**
 * How sure the browser is that it can play the file (R2-13, QA06 M5a-16):
 * - `probably`: `canPlayType` answers "probably", or it says no but Media Source Extensions support the type;
 * - `maybe`: `canPlayType` answers "maybe" (played, with a notice that it may not work, and the download);
 * - `no`: neither (the "can't play" message and the download).
 */
export type Playability = 'probably' | 'maybe' | 'no'

export function playability(
  mimeType: string,
  video: MediaElementProbe | undefined = defaultVideo(),
  mediaSource: MediaSourceProbe | undefined = globalThis.MediaSource,
): Playability {
  const answer = video?.canPlayType(mimeType) ?? ''
  if (answer === 'probably' || answer === 'maybe') {
    return answer
  }
  try {
    return mediaSource?.isTypeSupported(mimeType) ? 'probably' : 'no'
  } catch {
    return 'no'
  }
}
