import '@testing-library/jest-dom/vitest'

// The mock API's own tests run in Node (no DOM): the browser setup below is for component tests only.
if (typeof window !== 'undefined') {
  const { router } = await import('../lib/router/router.svelte')

  // jsdom doesn't implement scrolling; the router scrolls to the top on navigation.
  window.scrollTo = () => {}

  beforeEach(() => {
    history.replaceState(null, '', '/')
    router.sync()
    try {
      localStorage.clear()
    } catch {
      // Some tests make storage throw on purpose.
    }
  })
}
