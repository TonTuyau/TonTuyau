// The players' volume, as left last time in this browser (2.0, C1): the watch page's and the shorts' (2.0.1) share it.
import { readPreference, writePreference } from './storage'

export const VOLUME_KEY = 'tontuyau.volume'
export const MUTED_KEY = 'tontuyau.muted'

/** Gives a video the volume and mute saved last time (nothing saved: as it is, not muted). */
export function restoreVolume(media: HTMLMediaElement): void {
  const saved = readPreference(VOLUME_KEY)
  const value = Number(saved)
  if (saved !== null && value >= 0 && value <= 1) {
    media.volume = value
  }
  media.muted = readPreference(MUTED_KEY) === 'true'
}

/** Keeps a video's volume and mute for next time. */
export function saveVolume(media: HTMLMediaElement): void {
  writePreference(VOLUME_KEY, String(media.volume))
  writePreference(MUTED_KEY, String(media.muted))
}

/** Sets the volume (0 to 1; 0 is muted) and keeps it. */
export function setVolume(media: HTMLMediaElement, value: number): void {
  media.volume = Math.min(Math.max(value, 0), 1)
  media.muted = media.volume === 0
  saveVolume(media)
}

/** Mutes or unmutes (unmuted at 0: half the volume) and keeps it. */
export function toggleMute(media: HTMLMediaElement): void {
  media.muted = !media.muted
  if (!media.muted && media.volume === 0) {
    media.volume = 0.5
  }
  saveVolume(media)
}

/** The volume button's icon. */
export function volumeIcon(volume: number, muted: boolean): 'volumeOff' | 'volumeDown' | 'volumeUp' {
  return muted || volume === 0 ? 'volumeOff' : volume < 0.5 ? 'volumeDown' : 'volumeUp'
}
