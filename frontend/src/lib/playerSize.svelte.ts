// The watch page's player size (your M5 addition): "small", YouTube's default view (the player in the main column,
// the channel's other videos beside it), or "large", YouTube's theater view (the player across the whole width,
// the rest below). Chosen with the button on the player, kept in localStorage like the other UI preferences.
import { readPreference, writePreference } from './storage'

export type PlayerSize = 'small' | 'large'

export const PLAYER_SIZE_STORAGE_KEY = 'tontuyau.playerSize'

export function initialPlayerSize(): PlayerSize {
  return readPreference(PLAYER_SIZE_STORAGE_KEY) === 'large' ? 'large' : 'small'
}

class PlayerSizePreference {
  size = $state<PlayerSize>(initialPlayerSize())

  set(size: PlayerSize): void {
    this.size = size
    writePreference(PLAYER_SIZE_STORAGE_KEY, size)
  }

  toggle(): void {
    this.set(this.size === 'small' ? 'large' : 'small')
  }

  /** Re-reads the saved choice (tests, or another tab). */
  reload(): void {
    this.size = initialPlayerSize()
  }
}

export const playerSize = new PlayerSizePreference()
