// @vitest-environment node
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { cspViolations, cssViolations } from './check-csp-html.ts'

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
    ['a data: image (QA15-2 b)', '<img src="data:image/png;base64,iVBORw0KGgo=" alt="">'],
    ['a data: icon', "<link rel=icon href='data:image/svg+xml,%3Csvg%3E'>"],
    ['a data: srcset', '<img src="/x.png" srcset="data:image/png;base64,AAAA 2x" alt="">'],
  ])('reports %s', (_what, html) => {
    expect(cspViolations(`<!doctype html><body>${html}</body>`)).toHaveLength(1)
  })

  it('accepts links and images that merely mention data:', () => {
    expect(cspViolations('<a href="/search?q=data:">data:</a><img src="/art/data.png" alt="data:">')).toEqual([])
  })
})

describe('what the Content-Security-Policy would block in a stylesheet', () => {
  it('accepts url() to files, and ignores comments', () => {
    expect(cssViolations('.a { background: url(/assets/x-AbC.png) } /* url(data:image/png;base64,AA) */')).toEqual([])
  })

  it.each([
    ['an inlined image', '.a { background: url(data:image/png;base64,AAAA) }'],
    ['a quoted inlined font', "@font-face { src: url( 'data:font/woff2;base64,AAAA' ) }"],
  ])('reports %s', (_what, css) => {
    expect(cssViolations(css)).toHaveLength(1)
  })
})
