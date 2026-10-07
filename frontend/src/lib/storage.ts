// localStorage for UI preferences only. Every access is wrapped: private windows, blocked site data or a
// full quota must never break the app.

export function readPreference(key: string): string | null {
  try {
    return globalThis.localStorage?.getItem(key) ?? null
  } catch {
    return null
  }
}

export function writePreference(key: string, value: string): void {
  try {
    globalThis.localStorage?.setItem(key, value)
  } catch {
    // Not saved: the choice still applies for this visit.
  }
}
