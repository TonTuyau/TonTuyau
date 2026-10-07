// TonTuyau's look follows YouTube's layout, but its marks are its own: no YouTube red, no YouTube Shorts logo.
const theme =
  Object.values(
    import.meta.glob('./youtube.css', { query: '?raw', import: 'default', eager: true }) as Record<string, string>,
  )[0] ?? ''
const favicon =
  Object.values(
    import.meta.glob('/public/favicon.svg', { query: '?raw', import: 'default', eager: true }) as Record<
      string,
      string
    >,
  )[0] ?? ''
const icons =
  Object.values(
    import.meta.glob('../components/Icon.svelte', { query: '?raw', import: 'default', eager: true }) as Record<
      string,
      string
    >,
  )[0] ?? ''

const logo =
  Object.values(
    import.meta.glob('../components/Logo.svelte', { query: '?raw', import: 'default', eager: true }) as Record<
      string,
      string
    >,
  )[0] ?? ''

/** The shapes of an SVG mark: each path's `d` and each rect's geometry, in order. */
function shapes(svg: string): string[] {
  const mark = svg.slice(svg.indexOf('<svg'), svg.indexOf('</svg>'))
  return [...mark.matchAll(/<(path|rect)\b([^>]*)>/g)].map(([, tag, attributes = '']) => {
    const geometry = [...attributes.matchAll(/(?<=\s)(d|x|y|width|height|rx)="([^"]*)"/g)].map(
      ([, name, value]) => `${name}=${value}`,
    )
    return `${tag} ${geometry.join(' ')}`
  })
}

const youtubeReds = ['#ff0000', '#ff0033', '#e62117', '#cc0000', '#f00;']

describe('TonTuyau branding', () => {
  it('uses its own brand colour, not a YouTube red', () => {
    const brand = /--color-brand:\s*(#[0-9a-f]+)/i.exec(theme)?.[1]?.toLowerCase()

    expect(brand).toBe('#0f8b8d')
    expect(youtubeReds).not.toContain(brand)
  })

  it('has a favicon drawn in the brand colour, with no YouTube red', () => {
    expect(favicon).toContain('#0f8b8d')
    for (const red of youtubeReds) {
      expect(favicon.toLowerCase()).not.toContain(red)
    }
  })

  it('draws the favicon in one flat colour: the brand teal, nothing else', () => {
    const colours = new Set([...favicon.matchAll(/#[0-9a-f]{3,8}\b/gi)].map(([colour]) => colour.toLowerCase()))

    expect([...colours]).toEqual(['#0f8b8d'])
  })

  it('has the same mark in the top bar and as the favicon (the pipe, its two flanges, the play triangle)', () => {
    const mark = shapes(logo)

    expect(mark).toHaveLength(4)
    expect(shapes(favicon)).toEqual(mark)
  })

  it("doesn't use YouTube's Shorts logo as the Shorts icon", () => {
    expect(icons).not.toContain('M17.77 10.32')
  })
})
