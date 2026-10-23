// The listing grids on very wide screens : the page uses the whole width, the grids stop adding columns at a
// cap and the cards grow. jsdom doesn't lay out, so this checks the rules themselves; the widths were checked in a
// browser at 1280, 1920, 2560 and 3840 px (docs/releases/1.0.0.md).
const raw = (glob: Record<string, unknown>) => Object.values(glob).join('')
const theme = raw(import.meta.glob('./youtube.css', { query: '?raw', import: 'default', eager: true }))
const app = raw(import.meta.glob('../App.svelte', { query: '?raw', import: 'default', eager: true }))
const videoList = raw(
  import.meta.glob('../components/VideoList.svelte', { query: '?raw', import: 'default', eager: true }),
)
const skeleton = raw(
  import.meta.glob('../components/Skeleton.svelte', { query: '?raw', import: 'default', eager: true }),
)
const pages = import.meta.glob('../pages/{Channel,Channels,Search}Page.svelte', {
  query: '?raw',
  import: 'default',
  eager: true,
}) as Record<string, string>

/** A token's value, whitespace collapsed. */
function token(name: string): string {
  const match = new RegExp(`${name}:\\s*([^;]+);`).exec(theme)
  return (match?.[1] ?? '').replace(/\s+/g, ' ').replace(/\( /g, '(').replace(/ \)/g, ')').trim()
}

describe('listing grids on very wide screens', () => {
  it('caps the columns: 6 for videos, 10 for shorts', () => {
    expect(token('--grid-max-columns')).toBe('6')
    expect(token('--shorts-max-columns')).toBe('10')
  })

  it("makes a column at least the card's width and at least a capped share of the row (so cards grow past the cap)", () => {
    expect(token('--grid-columns')).toBe(
      'repeat(auto-fill, minmax(min(100%, max(var(--card-min-width), (100% - (var(--grid-max-columns) - 1) * ' +
        'var(--grid-gap-x)) / var(--grid-max-columns))), 1fr))',
    )
    expect(token('--shorts-columns')).toBe(
      'repeat(auto-fill, minmax(min(45%, max(var(--short-card-width), (100% - (var(--shorts-max-columns) - 1) * ' +
        'var(--shorts-gap-x)) / var(--shorts-max-columns))), 1fr))',
    )
  })

  it('the lists and their skeletons use those columns and gaps', () => {
    for (const source of [videoList, skeleton]) {
      expect(source).toContain('grid-template-columns: var(--grid-columns);')
      expect(source).toContain('grid-template-columns: var(--shorts-columns);')
      expect(source).toContain('var(--space-10) var(--grid-gap-x)')
    }

    expect(videoList).toContain('gap: var(--space-6) var(--shorts-gap-x);')
    expect(skeleton).toContain('column-gap: var(--shorts-gap-x);')
  })

  it('lets the page use the whole width: no cap on the main area any more', () => {
    const main = /\n\s*main\s*\{[^}]*\}/.exec(app)?.[0]

    expect(main).toContain('min-width: 0;')
    expect(main).not.toContain('max-width')
    expect(theme).not.toContain('--content-max-width')
  })

  it('keeps the channel, channels and search pages at their YouTube width', () => {
    expect(Object.keys(pages)).toHaveLength(3)
    for (const source of Object.values(pages)) {
      expect(source).toContain('max-width: var(--page-max-width);')
    }
    // 1284 px at the normal scale (2.1.0: in rem, scaled on big screens).
    expect(token('--page-max-width')).toBe('80.25rem')
  })
})
