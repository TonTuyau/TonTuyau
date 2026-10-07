// The shorts player preference (QA06, M5a-5): "vertical" (the default) opens shorts in the YouTube-like
// /shorts/<id> player; "normal" opens them on the regular watch page. Kept in localStorage like the theme.
import type { VideoCard } from './api/types'
import { href } from './router/routes'
import { readPreference, writePreference } from './storage'

export type ShortsPlayerPreference = 'vertical' | 'normal'

export const shortsPlayerPreferences: readonly ShortsPlayerPreference[] = ['vertical', 'normal']

export const SHORTS_PLAYER_STORAGE_KEY = 'tontuyau.shortsPlayer'

function isPreference(value: string | null): value is ShortsPlayerPreference {
  return value !== null && (shortsPlayerPreferences as readonly string[]).includes(value)
}

export function initialShortsPlayer(): ShortsPlayerPreference {
  const saved = readPreference(SHORTS_PLAYER_STORAGE_KEY)
  return isPreference(saved) ? saved : 'vertical'
}

class ShortsPlayer {
  preference = $state<ShortsPlayerPreference>(initialShortsPlayer())

  set(preference: ShortsPlayerPreference): void {
    this.preference = preference
    writePreference(SHORTS_PLAYER_STORAGE_KEY, preference)
  }

  /** Re-reads the saved choice (tests, or another tab). */
  reload(): void {
    this.preference = initialShortsPlayer()
  }
}

export const shortsPlayer = new ShortsPlayer()

/** Where a card opens: a short (the API's `type`) goes to the vertical player unless the user chose "normal". */
export function videoHref(video: Pick<VideoCard, 'id' | 'type'>): string {
  return video.type === 'short' && shortsPlayer.preference === 'vertical' ? href.short(video.id) : href.watch(video.id)
}
