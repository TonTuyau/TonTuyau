// Optional local layer (review item #2): the real channel names, subscribers, descriptions and pictures,
// read from a git-ignored folder (dev-data/mockup/, never committed, R2-4). Videos stay synthetic: copies of
// the committed synthetic videos, attached to the real channels. The tab counts come from the real counts.
import { existsSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import type { VideoCounts, VideoType } from '../src/lib/api/types.ts'
import type { ChannelFixture, MockDb, VideoFixture } from './db.ts'
import { bannerWidths, channelThumbWidths, versionsOf } from './imageSizes.ts'

/** Default: <repo>/dev-data/mockup, overridable with TONTUYAU_MOCK_REAL_DIR. */
export const realDir = resolve(
  process.env.TONTUYAU_MOCK_REAL_DIR ?? fileURLToPath(new URL('../../dev-data/mockup', import.meta.url)),
)

interface RealChannel {
  id: string
  name: string
  subscribers: number | null
  description: string | null
  tags: string[] | null
  thumb_path: string | null
  banner_path: string | null
  active: boolean
}

interface RealCounts {
  id: string
  counts: Partial<Record<VideoType, number>>
}

/** Synthetic cards generated per channel and type: enough to page (24 per page), not the real number. */
const CARDS_PER_TYPE = 30

export function hasRealLayer(dir: string = realDir): boolean {
  return existsSync(join(dir, 'channels.json')) && existsSync(join(dir, 'channel-video-counts.json'))
}

function hash(value: string): number {
  let h = 2166136261
  for (const char of value) {
    h = Math.imul(h ^ (char.codePointAt(0) ?? 0), 16777619)
  }
  return h >>> 0
}

const idChars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_'
function syntheticId(seed: string): string {
  let h = hash(seed)
  let id = 'r'
  for (let index = 0; index < 10; index++) {
    h = Math.imul(h ^ (h >>> 13), 0x5bd1e995) >>> 0
    id += idChars[h % idChars.length]
  }
  return id
}

function toChannel(real: RealChannel): ChannelFixture {
  const art = (path: string | null, kind: string) => (path ? `/art/channels/${real.id}/${kind}` : null)
  return {
    id: real.id,
    name: real.name,
    subscribers: real.subscribers ?? 0,
    thumbnailUrl: art(real.thumb_path, 'thumb'),
    // TubeArchivist's channel pictures are 900 px wide, its banners 2560 px (v1.4 version lists).
    thumbnails: versionsOf(art(real.thumb_path, 'thumb'), channelThumbWidths, 900),
    description: real.description ?? '',
    bannerUrl: art(real.banner_path, 'banner'),
    banners: versionsOf(art(real.banner_path, 'banner'), bannerWidths, 2560),
    tags: real.tags ?? [],
    active: real.active,
  }
}

function copyVideo(template: VideoFixture, id: string, channelId: string): VideoFixture {
  return {
    ...template,
    id,
    channelId,
    thumbnailUrl: template.thumbnailUrl === null ? null : `/art/videos/${id}`,
    mediaUrl: `/media/${id}`,
    downloadUrl: `/media/${id}/download`,
  }
}

/** The mock library with the real channels and synthetic videos attached to them. */
export function loadRealDb(synthetic: MockDb, dir: string = realDir): MockDb {
  const read = <T>(name: string): T => JSON.parse(readFileSync(join(dir, name), 'utf8')) as T
  const channels = read<RealChannel[]>('channels.json').map(toChannel)
  const videoCounts: Record<string, VideoCounts> = {}
  for (const entry of read<RealCounts[]>('channel-video-counts.json')) {
    videoCounts[entry.id] = {
      videos: entry.counts.video ?? 0,
      shorts: entry.counts.short ?? 0,
      streams: entry.counts.stream ?? 0,
    }
  }

  const pools: Record<VideoType, VideoFixture[]> = {
    video: synthetic.videos.filter((video) => video.type === 'video' && !video.id.startsWith('mock')),
    short: synthetic.videos.filter((video) => video.type === 'short' && !video.id.startsWith('mock')),
    stream: synthetic.videos.filter((video) => video.type === 'stream' && !video.id.startsWith('mock')),
  }
  const videos: VideoFixture[] = []
  for (const channel of channels) {
    const counts = videoCounts[channel.id] ?? { videos: 0, shorts: 0, streams: 0 }
    const wanted: Record<VideoType, number> = { video: counts.videos, short: counts.shorts, stream: counts.streams }
    for (const type of ['video', 'short', 'stream'] as const) {
      const pool = pools[type]
      const start = hash(`${channel.id}:${type}`)
      for (let index = 0; index < Math.min(wanted[type], CARDS_PER_TYPE) && pool.length > 0; index++) {
        const template = pool[(start + index) % pool.length] as VideoFixture
        videos.push(copyVideo(template, syntheticId(`${channel.id}:${type}:${index}`), channel.id))
      }
    }
  }

  // The edge-case videos keep their readable ids, attached to a real channel that has that type.
  const withType = (type: VideoType) =>
    channels.filter((channel) => {
      const counts = videoCounts[channel.id]
      return counts && (type === 'video' ? counts.videos : type === 'short' ? counts.shorts : counts.streams) > 0
    })
  for (const edge of synthetic.videos.filter((video) => video.id.startsWith('mock'))) {
    const candidates = withType(edge.type)
    const channel = candidates[hash(edge.id) % Math.max(1, candidates.length)] ?? channels[0]
    if (channel) {
      videos.push({ ...edge, channelId: channel.id })
    }
  }

  return { channels, videos, videoCounts, channelArtDir: join(dir, 'channel-art') }
}

/** The real picture for /art/channels/{id}/{kind}, or null. */
export function realChannelArt(db: MockDb, id: string, kind: string): string | null {
  if (!db.channelArtDir || !/^[\w-]+$/.test(id) || !['thumb', 'banner'].includes(kind)) {
    return null
  }
  const file = join(db.channelArtDir, `${id}_${kind}.jpg`)
  return existsSync(file) ? file : null
}
