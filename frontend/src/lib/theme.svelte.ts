// The appearance preference (review item #4): device theme (default, follows prefers-color-scheme), or a
// forced light / dark theme set as data-theme on <html>. The colours themselves all live in the theme file.
// index.html applies the saved choice before the first paint, with the same key and values.
import { readPreference, writePreference } from './storage'

export type ThemePreference = 'system' | 'light' | 'dark'

export const themePreferences: readonly ThemePreference[] = ['system', 'dark', 'light']

export const THEME_STORAGE_KEY = 'tontuyau.theme'

function isPreference(value: string | null): value is ThemePreference {
  return value !== null && (themePreferences as readonly string[]).includes(value)
}

export function applyTheme(preference: ThemePreference): void {
  if (typeof document === 'undefined') {
    return
  }
  if (preference === 'system') {
    delete document.documentElement.dataset.theme
  } else {
    document.documentElement.dataset.theme = preference
  }
}

class Theme {
  preference = $state<ThemePreference>(initialTheme())

  set(preference: ThemePreference): void {
    this.preference = preference
    writePreference(THEME_STORAGE_KEY, preference)
    applyTheme(preference)
  }
}

export function initialTheme(): ThemePreference {
  const saved = readPreference(THEME_STORAGE_KEY)
  return isPreference(saved) ? saved : 'system'
}

export const theme = new Theme()
applyTheme(theme.preference)
