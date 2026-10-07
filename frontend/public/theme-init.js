// Runs before the first paint, loaded by index.html as a plain blocking <script src> in <head>, not inline, so the
// Content-Security-Policy needs no hash (script-src 'self').

// Applies a forced light/dark theme (no white flash). Same key and values as src/lib/theme.svelte.ts; "system"
// (the default) sets nothing and follows prefers-color-scheme.
try {
  const theme = localStorage.getItem('tontuyau.theme')
  if (theme === 'dark' || theme === 'light') document.documentElement.dataset.theme = theme
} catch {
  // Storage blocked: device theme.
}
