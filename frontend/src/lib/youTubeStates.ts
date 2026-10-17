// A video's state on YouTube (1.5), as the pages say it: the admin pages' words for every state, and the badge
// visitors see for the three that change what they watch (gone, private, unlisted).
import type { YouTubeState } from './api/types'
import { t } from './i18n/i18n.svelte'

/** The states, in the order the admin pages list them. */
export const youTubeStates: YouTubeState[] = ['onYouTube', 'unlisted', 'private', 'retryLater', 'gone', 'notChecked']

type VisitorState = 'gone' | 'private' | 'unlisted'

export function isVisitorState(state: YouTubeState): state is VisitorState {
  return state === 'gone' || state === 'private' || state === 'unlisted'
}

/** The badge visitors see ("Gone from YouTube"…), and its explanation; null for the other states. */
export function visitorBadge(state: YouTubeState): { label: string; hint: string } | null {
  return isVisitorState(state) ? { label: t(`badge.${state}`), hint: t(`badge.${state}Hint`) } : null
}
