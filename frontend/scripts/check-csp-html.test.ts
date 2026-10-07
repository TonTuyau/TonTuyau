// @vitest-environment node
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { cspViolations } from './check-csp-html.ts'

describe('what the Content-Security-Policy would block in a page', () => {
  it('finds nothing in index.html (the theme script is a file, public/theme-init.js)', () => {
    expect(cspViolations(readFileSync(new URL('../index.html', import.meta.url), 'utf8'))).toEqual([])
  })

  it('accepts scripts and stylesheets loaded from files, and ignores comments', () => {
    const page = `<!doctype html><head>
      <script src="/theme-init.js"></script>
      <script type="module" crossorigin src="/assets/index-AbC.js"></script>
      <link rel="stylesheet" href="/assets/index-AbC.css">
      <!-- <script>alert(1)</script> -->
    </head><body><div id="app"></div></body>`

    expect(cspViolations(page)).toEqual([])
  })

  it.each([
    ['an inline script', '<script>document.title = "x"</script>'],
    ['an inline module', '<script type="module">import "/x.js"</script>'],
    ['code next to src', '<script src="/x.js">alert(1)</script>'],
    ['a style element', '<style>body { color: red }</style>'],
    ['a style attribute', '<div style="color: red"></div>'],
    ['an event handler', '<img src="/x.png" onerror="alert(1)">'],
  ])('reports %s', (_what, html) => {
    expect(cspViolations(`<!doctype html><body>${html}</body>`)).toHaveLength(1)
  })
})
