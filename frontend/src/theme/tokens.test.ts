// Swappable styling (R2-16): components only use the theme's tokens. This scans every component's <style>
// for raw colours, font families and pixel sizes (media queries excepted: they can't read custom properties).
// Read as text through a glob with a non-CSS query: Vitest empties plain CSS imports.
const theme = Object.values(
  import.meta.glob('./youtube.css', { query: '?raw', import: 'default', eager: true }) as Record<string, string>,
).join('')

const components = import.meta.glob('../**/*.svelte', { query: '?raw', import: 'default', eager: true }) as Record<
  string,
  string
>

function styles(source: string): string {
  return [...source.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)].map((match) => match[1] ?? '').join('\n')
}

function declarations(css: string): string[] {
  return css
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line !== '' && !line.startsWith('@media') && !line.startsWith('/*'))
}

describe('theme tokens', () => {
  const files = Object.entries(components)

  it('finds the components', () => {
    expect(files.length).toBeGreaterThan(20)
  })

  it.each(files)('%s uses no raw colour, font or pixel value', (_file, source) => {
    const offending = declarations(styles(source)).filter(
      (line) =>
        /#[0-9a-f]{3,8}\b/i.test(line) ||
        /\b(rgba?|hsla?|oklch|color-mix)\(/i.test(line) ||
        /font-family\s*:/.test(line) ||
        /\d+px\b/.test(line),
    )

    expect(offending).toEqual([])
  })

  it('every token a component uses is defined by the theme', () => {
    const defined = new Set([...theme.matchAll(/(--[\w-]+)\s*:/g)].map((match) => match[1]))
    // Component-local properties: set in its <style> or with style:--name={…}.
    const local = new Set(
      files.flatMap(([, source]) => [...source.matchAll(/(?:style:)?(--[\w-]+)\s*[:=]/g)].map((m) => m[1])),
    )
    const used = new Set(files.flatMap(([, source]) => [...source.matchAll(/var\((--[\w-]+)/g)].map((m) => m[1])))

    expect([...used].filter((token) => !defined.has(token) && !local.has(token))).toEqual([])
  })

  it('gives every theme-dependent colour a light and a dark value', () => {
    const colours = [...theme.matchAll(/(--color-[\w-]+):\s*([^;]+);/g)]
    const fixed = ['--color-brand', '--color-on-brand', '--color-overlay', '--color-on-overlay', '--color-scrim']
    const single = colours.filter(([, name, value]) => !value?.startsWith('light-dark(') && !fixed.includes(name ?? ''))

    expect(colours.length).toBeGreaterThan(20)
    expect(single.map(([, name]) => name)).toEqual(['--color-player-bg', '--color-on-player'])
  })

  it('lets data-theme force light or dark over the device setting', () => {
    expect(theme).toMatch(/color-scheme:\s*light dark/)
    expect(theme).toMatch(/:root\[data-theme='light'\]\s*\{\s*color-scheme:\s*light;/)
    expect(theme).toMatch(/:root\[data-theme='dark'\]\s*\{\s*color-scheme:\s*dark;/)
  })
})
