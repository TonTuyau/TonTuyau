// The admin area's left menu on wide screens: full (the default) or its icons only, the choice remembered. Narrower
// screens show the icons and the ☰ opens it for the visit only (AdminShell).
import { readPreference, writePreference } from './storage'

export const ADMIN_GUIDE_STORAGE_KEY = 'tontuyau.adminGuide'

class AdminGuide {
  collapsed = $state(readPreference(ADMIN_GUIDE_STORAGE_KEY) === 'collapsed')

  toggle(): void {
    this.collapsed = !this.collapsed
    writePreference(ADMIN_GUIDE_STORAGE_KEY, this.collapsed ? 'collapsed' : 'full')
  }

  /** Re-reads the saved choice (tests). */
  reload(): void {
    this.collapsed = readPreference(ADMIN_GUIDE_STORAGE_KEY) === 'collapsed'
  }
}

export const adminGuide = new AdminGuide()
