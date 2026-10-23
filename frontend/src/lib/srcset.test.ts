import { imageSizes, srcset, uiScales } from './srcset'

const theme =
  Object.values(
    import.meta.glob('../theme/youtube.css', { query: '?raw', import: 'default', eager: true }) as Record<
      string,
      string
    >,
  )[0] ?? ''

describe('srcset (resized art, contract v1.4)', () => {
  it("joins the API's versions as they come, URL and width", () => {
    expect(
      srcset([
        { width: 360, url: '/art/from-the-api/a?size=s' },
        { width: 720, url: '/x/y z' },
        { width: 1280, url: '/art/from-the-api/a' },
      ]),
    ).toBe('/art/from-the-api/a?size=s 360w, /x/y z 720w, /art/from-the-api/a 1280w')
  })

  it('has no srcset for an empty or absent list', () => {
    expect(srcset([])).toBeUndefined()
    expect(srcset(undefined)).toBeUndefined()
  })

  it('gives every layout a display width, the normal one last', () => {
    expect(imageSizes.grid).toMatch(/, \(min-width: 2600px\) calc\(\(100vw - 200px\) \/ 6\), 400px$/)
    expect(imageSizes.compact).toMatch(/, 168px$/)
    expect(imageSizes.row).toMatch(/^\(max-width: 600px\) 100vw, .*, 500px$/)
    expect(imageSizes.short).toMatch(/, \(min-width: 2192px\) calc\(\(100vw - 192px\) \* 256 \/ 810\), 633px$/)
    expect(Object.values(imageSizes.avatar).map((sizes) => sizes.split(', ').at(-1))).toEqual([
      '24px',
      '36px',
      '88px',
      '160px',
    ])
    expect(imageSizes.banner).toMatch(/^\(max-width: 1284px\) 100vw, .*, 1284px$/)
  })

  // 2.1.0: the theme scales the whole interface on big screens (the root's font size, every size in rem); the images
  // are shown that much wider, and `sizes` says so, with the theme's very media conditions.
  describe('big screens (the interface scaled)', () => {
    const steps = [...theme.matchAll(/@media ([^{]+) \{\s*:root \{\s*font-size: (\d+)%;/g)].map(([, when, size]) => ({
      when: when?.trim(),
      factor: Number(size) / 100,
    }))

    it('scales as the theme does: the same media conditions and factors', () => {
      expect([...steps].sort((a, b) => b.factor - a.factor)).toEqual([...uiScales])
    })

    it('asks for every image that much wider there, a big screen first', () => {
      expect(imageSizes.compact).toBe(
        '(min-width: 4800px) and (min-height: 2600px) 336px, (min-width: 3200px) and (min-height: 1800px) 252px, ' +
          '(min-width: 2400px) and (min-height: 1250px) 210px, 168px',
      )
      expect(imageSizes.avatar.lg).toContain('(min-width: 3200px) and (min-height: 1800px) 240px')
      // A 4K screen: the cards at least 1.5 × 400 px, or the capped column, the guide and gaps scaled too.
      expect(imageSizes.grid).toContain(
        '(min-width: 3200px) and (min-height: 1800px) max(600px, calc((100vw - 300px) / 6))',
      )
      expect(imageSizes.short).toContain(
        '(min-width: 3200px) and (min-height: 1800px) max(949.5px, calc((100vw - 288px) * 256 / 810))',
      )
    })
  })

  it('asks for short thumbnails at their drawn width: the 16:9 frame cropped into the portrait card', () => {
    const cardWidth = px('--short-card-width')
    const drawnWidth = Math.ceil((cardWidth * (16 / 9)) / (9 / 16))

    expect(cardWidth).toBe(200)
    expect(imageSizes.short).toMatch(
      new RegExp(`^\\(max-width: 600px\\) ${Math.round((50 * (16 / 9)) / (9 / 16))}vw, .*, ${drawnWidth}px$`),
    )
  })

  // The wide-screen parts, recomputed from the theme: past the column cap, a card is (container − gaps) / columns,
  // the container being the viewport minus the mini guide and the page's padding (an upper bound: the full guide
  // leaves less, and a bigger `sizes` only picks a sharper version).
  describe('very wide screens (the grids stop adding columns, the cards grow)', () => {
    const chrome = px('--sidebar-mini-width') + 2 * px('--space-6')

    it('grid cards: wider than 400 px from the viewport where the capped card passes 400 px', () => {
      const columns = Number(token('--grid-max-columns'))
      const minus = chrome + (columns - 1) * px('--grid-gap-x')
      const from = 400 * columns + minus

      expect([columns, minus, from]).toEqual([6, 200, 2600])
      expect(imageSizes.grid).toContain(`, (min-width: ${from}px) calc((100vw - ${minus}px) / ${columns}), 400px`)
    })

    it('short cards: drawn wider than 633 px from the viewport where the capped card passes 200 px', () => {
      const columns = Number(token('--shorts-max-columns'))
      const minus = chrome + (columns - 1) * px('--shorts-gap-x')
      const from = px('--short-card-width') * columns + minus

      expect([columns, minus, from]).toEqual([10, 192, 2192])
      // 16/9 ÷ 9/16 = 256/81, per column: 256 / (81 × 10).
      expect(imageSizes.short).toContain(`(min-width: ${from}px) calc((100vw - ${minus}px) * 256 / ${81 * columns})`)
    })

    it('the 4K example: a 3840 px viewport asks for 607 px grid cards and 1,153 px short frames', () => {
      expect(Math.round((3840 - 200) / 6)).toBe(607)
      expect(Math.round(((3840 - 192) * 256) / 810)).toBe(1153)
    })
  })
})

function token(name: string): string {
  const match = new RegExp(`${name}:\\s*([^;]+);`).exec(theme)
  if (!match?.[1]) throw new Error(`no ${name} in the theme`)
  return match[1].trim()
}

/** A length token in CSS px at the normal scale (rem: 16 px), following one var() hop (e.g. var(--space-4)). */
function px(name: string): number {
  const value = token(name)
  const alias = /^var\((--[\w-]+)\)$/.exec(value)?.[1]
  if (alias) return px(alias)
  const rem = /^([\d.]+)rem$/.exec(value)?.[1]
  return rem ? Number(rem) * 16 : Number(/^(\d+)px$/.exec(value)?.[1])
}
