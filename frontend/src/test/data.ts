// Small API-shaped objects for component tests. Values are deliberately odd (URLs the front could never
// guess, counts that don't match list lengths) so a test fails if a component computes instead of displaying.
import type { ChannelCard, ChannelDetail, Page, VideoCard, VideoDetail } from '../lib/api/types'

export function videoCard(overrides: Partial<VideoCard> = {}): VideoCard {
  return {
    id: 'vid00000001',
    title: 'Crème brûlée : la recette',
    type: 'video',
    published: '2024-04-24T15:01:48Z',
    durationSeconds: 1914,
    views: 74_198,
    active: true,
    thumbnailUrl: '/art/from-the-api/vid00000001.webp',
    thumbnails: [
      { width: 360, url: '/art/from-the-api/vid00000001/small' },
      { width: 1280, url: '/art/from-the-api/vid00000001.webp' },
    ],
    channel: {
      id: 'UCtest',
      name: 'Tom Cooks',
      thumbnailUrl: '/art/from-the-api/UCtest-thumb',
      thumbnails: [{ width: 900, url: '/art/from-the-api/UCtest-thumb' }],
    },
    ...overrides,
  }
}

export function videoDetail(overrides: Partial<VideoDetail> = {}): VideoDetail {
  return {
    ...videoCard(),
    channel: {
      id: 'UCtest',
      name: 'Tom Cooks',
      thumbnailUrl: '/art/from-the-api/UCtest-thumb',
      thumbnails: [{ width: 900, url: '/art/from-the-api/UCtest-thumb' }],
      subscribers: 224_000,
    },
    description: 'Line one\nLine two',
    downloaded: '2026-10-01T07:13:01Z',
    likes: 3166,
    tags: ['cuisine', 'dessert'],
    categories: ['Howto & Style'],
    fileSize: 1_313_668_754,
    mimeType: 'video/mp4; codecs="av01.0.08M.08, opus"',
    tracks: [
      { kind: 'video', codec: 'av1', width: 3840, height: 2160, bitrate: 5_370_710 },
      { kind: 'audio', codec: 'opus', width: null, height: null, bitrate: 110_978 },
    ],
    mediaUrl: '/media/from-the-api/vid00000001',
    downloadUrl: '/media/from-the-api/vid00000001/download',
    mediaAvailable: true,
    ...overrides,
  }
}

export function channelCard(overrides: Partial<ChannelCard> = {}): ChannelCard {
  return {
    id: 'UCtest',
    name: 'Tom Cooks',
    subscribers: 224_000,
    thumbnailUrl: '/art/from-the-api/UCtest-thumb',
    thumbnails: [
      { width: 48, url: '/art/from-the-api/UCtest-thumb/tiny' },
      { width: 900, url: '/art/from-the-api/UCtest-thumb' },
    ],
    videoCounts: { videos: 120, shorts: 14, streams: 0 },
    ...overrides,
  }
}

export function channelDetail(overrides: Partial<ChannelDetail> = {}): ChannelDetail {
  return {
    ...channelCard(),
    description: 'A cooking channel.',
    bannerUrl: '/art/from-the-api/UCtest-banner',
    banners: [
      { width: 1280, url: '/art/from-the-api/UCtest-banner/1280' },
      { width: 2560, url: '/art/from-the-api/UCtest-banner' },
    ],
    tags: ['Cuisine'],
    active: true,
    ...overrides,
  }
}

export function page<T>(items: T[], total = items.length, nextCursor: string | null = null): Page<T> {
  return { items, total, nextCursor }
}
