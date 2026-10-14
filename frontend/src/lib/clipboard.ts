// Copying a text (1.4: a run's id, its log). The browser may refuse (no permission, an old browser, not HTTPS).

/** @returns Whether it's copied. */
export async function copyText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text)
    return true
  } catch {
    // Refused: the caller says so, the text stays on screen to select.
    return false
  }
}
