// The left guide's expanded/collapsed state (review item #5): collapsed (the mini rail) by default,
// the user's toggle remembered.
import { readPreference, writePreference } from './storage'

export const GUIDE_STORAGE_KEY = 'tontuyau.guide'

class Guide {
  expanded = $state(readPreference(GUIDE_STORAGE_KEY) === 'expanded')

  toggle(): void {
    this.expanded = !this.expanded
    writePreference(GUIDE_STORAGE_KEY, this.expanded ? 'expanded' : 'collapsed')
  }

  /** Re-reads the saved choice (tests, or another tab). */
  reload(): void {
    this.expanded = readPreference(GUIDE_STORAGE_KEY) === 'expanded'
  }
}

export const guide = new Guide()
