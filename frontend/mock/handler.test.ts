// @vitest-environment node
// The mock API must answer like the contract says, or the mockup would show things the real backend won't.
import { describe, expect, it } from 'vitest'
import type {
  ChannelCard,
  ChannelDetail,
  Home,
  Page,
  ProblemDetails,
  SearchAll,
  Suggestions,
  VideoCard,
  VideoDetail,
} from '../src/lib/api/types.ts'
import { artFor } from './art.ts'
import { emptyDb, loadDb } from './db.ts'
import { fold, handleApi } from './handler.ts'

const db = loadDb()

function get<T>(path: string, status = 200): T {
  const response = handleApi(db, 'GET', new URL(path, 'http://mock.local'))
  expect(response.status, `${path} → ${response.body}`).toBe(status)
  return JSON.parse(response.body) as T
}

function walk<T>(path: string): T[] {
  const items: T[] = []
  let page = get<Page<T>>(path)
  items.push(...page.items)
  while (page.nextCursor) {
    const separator = path.includes('?') ? '&' : '?'
    page = get<Page<T>>(`${path}${separator}cursor=${encodeURIComponent(page.nextCursor)}`)
    items.push(...page.items)
  }
  return items
}

describe('mock fixtures', () => {
  it('hold the library the brief asks for', () => {
    expect(db.channels.length).toBeGreaterThanOrEqual(10)
    expect(db.videos.length).toBeGreaterThanOrEqual(150)
    for (const type of ['video', 'short', 'stream']) {
      expect(db.videos.some((video) => video.type === type)).toBe(true)
    }
    expect(db.videos.some((video) => !video.active)).toBe(true)
    expect(db.videos.some((video) => !video.mediaAvailable)).toBe(true)
    expect(new Set(db.videos.map((video) => video.id)).size).toBe(db.videos.length)
    // A MIME type Chromium answers "maybe" for (M5a-16), on a file that plays.
    expect(db.videos.find((video) => video.id === 'mockMaybe00')).toMatchObject({
      mimeType: 'video/mp4; codecs="avc1"',
      mediaAvailable: true,
    })
    // A description with links, markup and timestamps (linkify, M5a-7).
    expect(db.videos.find((video) => video.id === 'mockLinks00')?.description).toMatch(/https:\/\/.*<b>.*\n00:00/s)
  })

  it('only reference channels that exist', () => {
    const ids = new Set(db.channels.map((channel) => channel.id))
    expect(db.videos.filter((video) => !ids.has(video.channelId))).toEqual([])
  })
})

describe('mock /api/home', () => {
  it('returns the sections in the order video, short, stream, latest published first', () => {
    const home = get<Home>('/api/home?size=5')

    expect(home.sections.map((section) => section.type)).toEqual(['video', 'short', 'stream'])
    for (const section of home.sections) {
      expect(section.items).toHaveLength(5)
      const dates = section.items.map((item) => item.published)
      expect(dates).toEqual([...dates].sort().reverse())
    }
  })

  it('omits empty sections', () => {
    const response = handleApi(emptyDb, 'GET', new URL('http://mock.local/api/home'))

    expect(JSON.parse(response.body)).toEqual({ sections: [] })
  })
})

describe('mock lists and paging', () => {
  it('walks every video exactly once with the cursors, total exact', () => {
    const first = get<Page<VideoCard>>('/api/videos?size=7')
    const all = walk<VideoCard>('/api/videos?size=7')

    expect(first.total).toBe(db.videos.length)
    expect(all).toHaveLength(db.videos.length)
    expect(new Set(all.map((video) => video.id)).size).toBe(db.videos.length)
  })

  it('leaves the `exclude` video out of the library list and of its total (v1.3)', () => {
    const path = '/api/videos?type=short&sort=latest&size=4'
    const all = walk<VideoCard>(path)
    const excluded = all[1]?.id ?? ''
    const full = get<Page<VideoCard>>(path)
    const without = get<Page<VideoCard>>(`${path}&exclude=${excluded}`)
    const walked = walk<VideoCard>(`${path}&exclude=${excluded}`)

    expect(without.total).toBe(full.total - 1)
    expect(walked.map((video) => video.id)).toEqual(all.map((video) => video.id).filter((id) => id !== excluded))
    expect(get<Page<VideoCard>>(`${path}&exclude=unknownVid1`).total).toBe(full.total)
  })

  it('refuses a library cursor made without the same `exclude`', () => {
    const path = '/api/videos?type=short&size=4'
    const page = get<Page<VideoCard>>(`${path}&exclude=unknownVid1`)

    expect(get<ProblemDetails>(`${path}&cursor=${page.nextCursor}`, 400).errors).toHaveProperty('cursor')
    expect(get<ProblemDetails>(`${path}&exclude=unknownVid2&cursor=${page.nextCursor}`, 400).errors).toHaveProperty(
      'cursor',
    )
  })

  it('refuses a cursor made for another sort', () => {
    const page = get<Page<VideoCard>>('/api/videos?size=5&sort=popular')
    const problem = get<ProblemDetails>(`/api/videos?sort=oldest&cursor=${page.nextCursor}`, 400)

    expect(problem.errors).toHaveProperty('cursor')
  })

  it('answers an invalid parameter with a ProblemDetails 400', () => {
    const problem = get<ProblemDetails>('/api/channels/UCmock-cuisine-margaux0/videos?sort=newest', 400)

    expect(problem).toMatchObject({ status: 400, title: 'Invalid request', errors: { sort: [expect.any(String)] } })
    expect(problem.detail).toContain('latest, popular, oldest')
  })

  it('answers an unknown id with a 404', () => {
    expect(get<ProblemDetails>('/api/videos/unknownVid1', 404).status).toBe(404)
    expect(get<ProblemDetails>('/api/channels/UCunknownunknownunknown1', 404).status).toBe(404)
  })

  it('answers a malformed id with a 400, like the backend (M4-Q7)', () => {
    expect(get<ProblemDetails>('/api/videos/nope', 400).errors).toHaveProperty('id')
    expect(get<ProblemDetails>('/api/channels/UCnope', 400).errors).toHaveProperty('id')
    expect(get<ProblemDetails>('/api/channels/UCnope/videos?type=video', 400).errors).toHaveProperty('id')
    expect(get<ProblemDetails>('/api/videos?exclude=x', 400).errors).toHaveProperty('exclude')
    expect(get<ProblemDetails>('/api/search?q=a&channel=nope', 400).errors).toHaveProperty('channel')
  })

  it('keeps the fixture ids valid: every video id, and every channel id but the three M5a review ones', () => {
    const legacy = ['UCmock-cuisine-margaux0', 'UCmock-atelier-bois-fer', 'UCmock-chroniques-eleonore']

    expect(db.videos.filter((video) => !/^[A-Za-z0-9_-]{11}$/.test(video.id))).toEqual([])
    expect(db.channels.map((channel) => channel.id).filter((id) => !/^UC[A-Za-z0-9_-]{22}$/.test(id))).toEqual(legacy)
  })

  it('measures text limits on the trimmed text, like the backend', () => {
    expect(get<ProblemDetails>('/api/search?q=%20%20', 400).errors).toHaveProperty('q')
    expect(get<ProblemDetails>(`/api/channels?q=${'a'.repeat(101)}`, 400).errors).toHaveProperty('q')
    expect(get<Page<ChannelCard>>(`/api/channels?q=${encodeURIComponent(` ${'a'.repeat(100)} `)}`).total).toBe(0)
  })

  it('sorts videos by views for popular', () => {
    const views = get<Page<VideoCard>>('/api/videos?sort=popular&size=20').items.map((video) => video.views)

    expect(views).toEqual([...views].sort((a, b) => b - a))
  })
})

describe('mock channels', () => {
  it('counts videos per type (the backend does that, not the front)', () => {
    const channel = get<ChannelDetail>('/api/channels/UCmock-minute-meca000000')
    const shorts = db.videos.filter((video) => video.channelId === channel.id && video.type === 'short')

    expect(channel.videoCounts).toEqual({ videos: 0, shorts: shorts.length, streams: 0 })
  })

  it('filters by name as you type, accents ignored', () => {
    const names = get<Page<ChannelCard>>('/api/channels?q=eleo').items.map((channel) => channel.name)

    expect(names).toEqual(["Les Chroniques d'Éléonore"])
  })

  it('leaves the `exclude` video out of a channel’s list and of its total (v1.1)', () => {
    const path = '/api/channels/UCmock-pixelforge-gaming/videos?type=video&size=5'
    const all = walk<VideoCard>(path)
    const excluded = all[2]?.id ?? ''
    const full = get<Page<VideoCard>>(path)
    const without = get<Page<VideoCard>>(`${path}&exclude=${excluded}`)
    const walked = walk<VideoCard>(`${path}&exclude=${excluded}`)

    expect(without.total).toBe(full.total - 1)
    expect(walked.map((video) => video.id)).toEqual(all.map((video) => video.id).filter((id) => id !== excluded))
  })

  it('ignores an `exclude` that is not in the list', () => {
    const path = '/api/channels/UCmock-pixelforge-gaming/videos?type=video'

    expect(get<Page<VideoCard>>(`${path}&exclude=unknownVid1`).total).toBe(get<Page<VideoCard>>(path).total)
  })

  it('refuses a cursor made without the same `exclude`', () => {
    const path = '/api/channels/UCmock-pixelforge-gaming/videos?type=video&size=5'
    const page = get<Page<VideoCard>>(`${path}&exclude=unknownVid1`)

    expect(get<ProblemDetails>(`${path}&cursor=${page.nextCursor}`, 400).errors).toHaveProperty('cursor')
  })

  it('lists a channel’s videos of one type only', () => {
    const items = walk<VideoCard>('/api/channels/UCmock-pixelforge-gaming/videos?type=stream')

    expect(items.length).toBeGreaterThan(0)
    expect(items.every((video) => video.type === 'stream' && video.channel.id === 'UCmock-pixelforge-gaming')).toBe(
      true,
    )
  })
})

describe('mock search', () => {
  it('ignores accents: "creme" finds "crème"', () => {
    const page = get<Page<VideoCard>>('/api/search?q=creme&kind=videos')

    expect(page.total).toBeGreaterThan(0)
    expect(page.items.some((video) => video.title.includes('crème'))).toBe(true)
    expect(page.items.every((video) => fold(video.title).includes('creme'))).toBe(true)
  })

  it('kind=all gives up to 3 channels on the first page, and `channels: []` (not absent) after', () => {
    const first = get<SearchAll>('/api/search?q=cuisine&size=2')

    expect(first.channels.length).toBeGreaterThan(0)
    expect(first.channels.length).toBeLessThanOrEqual(3)
    expect(first.videos.nextCursor).not.toBeNull()
    const next = get<SearchAll>(`/api/search?q=cuisine&size=2&cursor=${first.videos.nextCursor}`)
    expect(next).toHaveProperty('channels')
    expect(next.channels).toEqual([])
  })

  describe('published period (v1.2)', () => {
    const DAY = 86_400_000
    const latest = Math.max(...db.videos.map((video) => Date.parse(video.published)))
    // A clock two days after the newest fixture video.
    const now = latest + 2 * DAY
    const at = <T>(path: string, clock: number, status = 200): T => {
      const response = handleApi(db, 'GET', new URL(path, 'http://mock.local'), clock)
      expect(response.status, `${path} → ${response.body}`).toBe(status)
      return JSON.parse(response.body) as T
    }

    it.each([
      ['today', 1],
      ['week', 7],
      ['month', 30],
      ['year', 365],
    ] as const)('"%s" keeps the videos published within the last %i days', (period, days) => {
      const page = at<Page<VideoCard>>(`/api/search?q=a&kind=videos&published=${period}&size=100`, now)
      const expected = db.videos.filter((video) => Date.parse(video.published) >= now - days * DAY)

      expect(page.items.every((video) => Date.parse(video.published) >= now - days * DAY)).toBe(true)
      expect(page.total).toBeLessThanOrEqual(expected.length)
    })

    it('keeps the first page’s cut-off in the cursor', () => {
      const path = '/api/search?q=a&kind=videos&published=year&sort=latest&size=3'
      const first = at<Page<VideoCard>>(path, now)
      expect(first.nextCursor).not.toBeNull()

      // A month later, the next page still uses the first page's cut-off: same total, no video dropped.
      const next = at<Page<VideoCard>>(`${path}&cursor=${first.nextCursor}`, now + 30 * DAY)
      const fresh = at<Page<VideoCard>>(path, now + 30 * DAY)

      expect(next.total).toBe(first.total)
      expect(fresh.total).toBeLessThan(first.total)
    })

    it('refuses an unknown period, and a cursor made without the period', () => {
      expect(at<ProblemDetails>('/api/search?q=a&published=decade', now, 400).errors).toHaveProperty('published')
      const plain = at<Page<VideoCard>>('/api/search?q=a&kind=videos&size=3', now)
      const problem = at<ProblemDetails>(
        `/api/search?q=a&kind=videos&size=3&published=week&cursor=${plain.nextCursor}`,
        now,
        400,
      )
      expect(problem.errors).toHaveProperty('cursor')
    })
  })

  it('applies the duration filter', () => {
    const page = get<Page<VideoCard>>('/api/search?q=a&kind=videos&duration=long&size=100')

    expect(page.items.every((video) => video.durationSeconds > 1200)).toBe(true)
  })

  it('requires q', () => {
    expect(get<ProblemDetails>('/api/search', 400).errors).toHaveProperty('q')
  })

  it('suggests at most 3 channels, then video titles fill the rest (v1.1)', () => {
    // A one-letter prefix that matches more than 3 channel names.
    const prefix = [...'abcdefghijklmnopqrstuvwxyz'].find(
      (letter) =>
        db.channels.filter((channel) =>
          fold(channel.name)
            .split(/\W+/)
            .some((w) => w.startsWith(letter)),
        ).length > 3,
    )
    expect(prefix).toBeDefined()
    const { suggestions } = get<Suggestions>(`/api/search/suggest?q=${prefix}&size=8`)
    const kinds = suggestions.map((suggestion) => suggestion.kind)

    expect(kinds.filter((kind) => kind === 'channel')).toHaveLength(3)
    expect(kinds.slice(0, 3)).toEqual(['channel', 'channel', 'channel'])
    expect(kinds.slice(3).every((kind) => kind === 'video')).toBe(true)
    expect(suggestions).toHaveLength(8)
  })

  it('suggests channels first, then video titles, by prefix', () => {
    const { suggestions } = get<Suggestions>('/api/search/suggest?q=pix&size=5')

    expect(suggestions[0]).toEqual({ text: 'PixelForge Gaming', kind: 'channel', id: 'UCmock-pixelforge-gaming' })
    expect(suggestions.slice(1).every((suggestion) => suggestion.kind === 'video')).toBe(true)
    expect(suggestions.length).toBeLessThanOrEqual(5)
  })
})

describe('mock videos and art', () => {
  it('joins the channel into a video detail', () => {
    const video = get<VideoDetail>('/api/videos/mockNoFile0')

    expect(video.channel).toEqual({
      id: 'UCmock-cuisine-margaux0',
      name: 'Cuisine de Margaux',
      thumbnailUrl: '/art/channels/UCmock-cuisine-margaux0/thumb',
      thumbnails: [48, 96, 176, 320]
        .map((width) => ({
          width,
          url: `/art/channels/UCmock-cuisine-margaux0/thumb/${width}`,
        }))
        .concat({ width: 512, url: '/art/channels/UCmock-cuisine-margaux0/thumb' }),
      subscribers: 224_000,
    })
    expect(video.mediaAvailable).toBe(false)
    expect(video).not.toHaveProperty('channelId')
  })

  it('serves an SVG for a known thumbnail and nothing for the 404 edge cases', () => {
    expect(artFor(db, '/art/videos/mockHugeVws')).toContain('<svg')
    expect(artFor(db, '/art/videos/mockArt404x')).toBeNull()
    expect(artFor(db, '/art/videos/mockNoThumb')).toBeNull()
    expect(artFor(db, '/art/channels/UCmock-quiet-trails00000/banner')).toBeNull()
  })

  it('has no TV art anywhere (v1.1): no tvArtUrl, no tvart image kind', () => {
    const channel = get<ChannelDetail>('/api/channels/UCmock-cuisine-margaux0')

    expect(channel).not.toHaveProperty('tvArtUrl')
    expect(db.channels.some((fixture) => 'tvArtUrl' in fixture)).toBe(false)
    expect(artFor(db, '/art/channels/UCmock-cuisine-margaux0/thumb')).toContain('<svg')
    expect(artFor(db, '/art/channels/UCmock-cuisine-margaux0/tvart')).toBeNull()
  })

  describe('resized art (contract v1.4)', () => {
    const sorted = (widths: number[]) => [...widths].sort((a, b) => a - b)

    it('lists each image with its versions, smallest first, the original last', () => {
      const card = get<Page<VideoCard>>('/api/videos?size=100').items
      for (const video of card) {
        const widths = video.thumbnails.map((size) => size.width)
        expect(widths).toEqual(sorted(widths))
        if (video.thumbnailUrl === null) {
          expect(video.thumbnails).toEqual([])
        } else {
          expect(video.thumbnails.at(-1)?.url).toBe(video.thumbnailUrl)
        }
      }
      const channel = get<ChannelDetail>('/api/channels/UCmock-pixelforge-gaming')
      expect(channel.thumbnails.map((size) => size.width)).toEqual([48, 96, 176, 320, 512])
      expect(channel.banners).toEqual([
        { width: 1280, url: '/art/channels/UCmock-pixelforge-gaming/banner/1280' },
        { width: 2560, url: '/art/channels/UCmock-pixelforge-gaming/banner' },
      ])
    })

    it('has the edge cases: no image, versions not made yet, a short (720 px original)', () => {
      expect(get<VideoDetail>('/api/videos/mockNoThumb').thumbnails).toEqual([])
      expect(get<VideoDetail>('/api/videos/mockOrigOnl').thumbnails).toEqual([
        { width: 1280, url: '/art/videos/mockOrigOnl' },
      ])
      expect(get<VideoDetail>('/api/videos/mockShort01').thumbnails.map((size) => size.width)).toEqual([360, 720])
    })

    it('draws every listed version at its width, and nothing else', () => {
      expect(artFor(db, '/art/videos/mockHugeVws/360')).toContain('width="360"')
      expect(artFor(db, '/art/videos/mockHugeVws/720')).toContain('width="720"')
      expect(artFor(db, '/art/channels/UCmock-pixelforge-gaming/thumb/48')).toContain('width="48"')
      expect(artFor(db, '/art/channels/UCmock-pixelforge-gaming/banner/1280')).toContain('width="1280"')
      expect(artFor(db, '/art/videos/mockHugeVws/361')).toBeNull()
      expect(artFor(db, '/art/videos/mockHugeVws/1280')).toBeNull()
      expect(artFor(db, '/art/videos/mockOrigOnl/360')).toBeNull()
      expect(artFor(db, '/art/videos/mockShort01/720')).toBeNull()
      expect(artFor(db, '/art/channels/UCmock-pixelforge-gaming/banner/48')).toBeNull()
      expect(artFor(db, '/art/channels/UCmock-pixelforge-gaming/thumb/48/x')).toBeNull()
    })
  })
})
