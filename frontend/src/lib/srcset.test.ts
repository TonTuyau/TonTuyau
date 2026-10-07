import { imageSizes, srcset } from './srcset'

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

  it('gives every layout a display width', () => {
    expect(imageSizes.grid).toBe('(max-width: 600px) 100vw, (min-width: 2600px) calc((100vw - 200px) / 6), 400px')
    expect(imageSizes.compact).toBe('168px')
    expect(imageSizes.row).toBe('(max-width: 600px) 100vw, 500px')
    expect(imageSizes.short).toBe(
      '(max-width: 600px) 158vw, (min-width: 2192px) calc((100vw - 192px) * 256 / 810), 633px',
    )
    expect(imageSizes.avatar).toEqual({ xs: '24px', sm: '36px', md: '88px', lg: '160px' })
    expect(imageSizes.banner).toBe('(max-width: 1284px) 100vw, 1284px')
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
      expect(imageSizes.grid).toBe(
        `(max-width: 600px) 100vw, (min-width: ${from}px) calc((100vw - ${minus}px) / ${columns}), 400px`,
      )
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

/** A pixel token, following one var() hop (e.g. --grid-gap-x: var(--space-4)). */
function px(name: string): number {
  const value = token(name)
  const alias = /^var\((--[\w-]+)\)$/.exec(value)?.[1]
  return alias ? px(alias) : Number(/^(\d+)px$/.exec(value)?.[1])
}
