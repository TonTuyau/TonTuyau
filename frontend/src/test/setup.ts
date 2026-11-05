import '@testing-library/jest-dom/vitest'

// The mock API's own tests run in Node (no DOM): the browser setup below is for component tests only.
if (typeof window !== 'undefined') {
  const { router } = await import('../lib/router/router.svelte')
  const { auth } = await import('../lib/auth.svelte')

  // jsdom doesn't implement scrolling; the router scrolls to the top on navigation.
  window.scrollTo = () => {}
  // Nor media playback: the player pauses and empties a <video> it lets go of (tests spy on these when it matters).
  HTMLMediaElement.prototype.load = function () {}
  HTMLMediaElement.prototype.pause = function () {}
  HTMLMediaElement.prototype.play = () => Promise.resolve()
  // Nor a <dialog>'s modal (2.3.0): open and closed as a plain one, closing says so as the browser does.
  HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
    this.open = true
  }
  HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) {
    this.open = false
    this.dispatchEvent(new Event('close'))
  }

  beforeEach(() => {
    history.replaceState(null, '', '/')
    router.sync()
    // Each test's app asks again who is logged in (its own fake API answers).
    auth.reset()
    try {
      localStorage.clear()
    } catch {
      // Some tests make storage throw on purpose.
    }
  })
}
