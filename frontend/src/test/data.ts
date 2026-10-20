// Small API-shaped objects for component tests. Values are deliberately odd (URLs the front could never
// guess, counts that don't match list lengths) so a test fails if a component computes instead of displaying.
import type {
  ApiKey,
  ChannelCard,
  ChannelDetail,
  ChannelTotals,
  Dashboard,
  ImportHistory,
  Page,
  Run,
  Tasks,
  VideoCard,
  VideoDetail,
} from '../lib/api/types'

export function videoCard(overrides: Partial<VideoCard> = {}): VideoCard {
  return {
    id: 'vid00000001',
    title: 'Crème brûlée : la recette',
    type: 'video',
    published: '2024-04-24T15:01:48Z',
    durationSeconds: 1914,
    views: 74_198,
    active: true,
    youTubeState: 'onYouTube',
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
    chapters: [],
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

export function channelTotals(overrides: Partial<ChannelTotals> = {}): ChannelTotals {
  return {
    id: 'UCtest',
    name: 'Tom Cooks',
    thumbnailUrl: '/art/from-the-api/UCtest-thumb',
    thumbnails: [{ width: 900, url: '/art/from-the-api/UCtest-thumb' }],
    counts: { videos: 120, shorts: 14, streams: 0 },
    gone: 3,
    private: 0,
    unlisted: 0,
    retryLater: 5,
    notChecked: 0,
    total: 134,
    ...overrides,
  }
}

/** The admin dashboard. The shares and averages don't follow from the other numbers: the page must show them as is. */
export function dashboard(overrides: Partial<Dashboard> = {}): Dashboard {
  return {
    lastSync: '2026-10-12T07:00:00Z',
    totals: { channels: 3, videos: 5583, shorts: 1231, streams: 574, gone: 85 },
    playTime: {
      totalSeconds: 3_600_000,
      types: [
        { type: 'video', count: 5583, seconds: 2_520_000, share: 0.7, averageSeconds: 451 },
        { type: 'short', count: 1231, seconds: 36_000, share: 0.01, averageSeconds: 29 },
        { type: 'stream', count: 574, seconds: 1_044_000, share: 0.29, averageSeconds: 7200 },
      ],
    },
    channels: [
      channelTotals({
        id: 'UCbig',
        name: 'Vinyl Archive',
        counts: { videos: 1203, shorts: 0, streams: 156 },
        gone: 24,
        total: 1359,
      }),
      channelTotals({
        id: 'UCmid',
        name: 'Écoles du Goût',
        counts: { videos: 412, shorts: 96, streams: 12 },
        gone: 0,
        total: 520,
      }),
      channelTotals({
        id: 'UCsmall',
        name: 'Atelier Bois',
        counts: { videos: 254, shorts: 61, streams: 3 },
        gone: 7,
        total: 318,
      }),
    ],
    ...overrides,
  }
}

/**
 * An import history by day in Paris: three days, the first of them a run of 1.1 (no types), the second empty. The
 * starts are Paris midnights (UTC+2 in October).
 */
export function importHistory(overrides: Partial<ImportHistory> = {}): ImportHistory {
  const none = { videos: 0, shorts: 0, streams: 0 }
  return {
    step: 'day',
    timeZone: 'Europe/Paris',
    total: 40_012,
    bars: [
      { start: '2026-10-09T22:00:00Z', total: 40_000, byType: none, notRecorded: 40_000 },
      { start: '2026-10-10T22:00:00Z', total: 0, byType: none, notRecorded: 0 },
      { start: '2026-10-11T22:00:00Z', total: 12, byType: { videos: 8, shorts: 3, streams: 1 }, notRecorded: 0 },
    ],
    ...overrides,
  }
}

/** A run of the history (2.0): the import when the backend started, succeeded. */
export function apiKey(overrides: Partial<ApiKey> = {}): ApiKey {
  return {
    id: 7,
    name: 'Firefox, laptop',
    prefix: 'tt_k7Qm',
    kind: 'browserExtension',
    enabled: true,
    createdAt: '2026-10-02T08:30:00Z',
    createdBy: 'claire',
    lastUsedAt: null,
    ...overrides,
  }
}

export function run(overrides: Partial<Run> = {}): Run {
  return {
    id: 'run-20261012T120000Z-3f9a1c',
    task: 'import',
    trigger: 'startup',
    startedBy: null,
    state: 'succeeded',
    startedAt: '2026-10-12T12:00:00Z',
    queuedAt: '2026-10-12T12:00:00Z',
    position: null,
    finishedAt: '2026-10-12T12:00:07Z',
    error: null,
    counts: {
      videosRead: 61,
      channelsRead: 0,
      created: 8,
      updated: 3,
      unchanged: 50,
      rejected: 0,
      corrected: 0,
      artCopied: 8,
      artFailed: 0,
      artResized: 24,
      artResizeFailed: 0,
      mediaMissing: 0,
      foundOnYouTube: 0,
      unlistedOnYouTube: 0,
      goneFromYouTube: 0,
      privateOnYouTube: 0,
      checkFailed: 0,
      commentsRead: 0,
    },
    target: null,
    resumeAt: null,
    left: 0,
    indexRebuild: false,
    notes: [],
    cancellable: false,
    ...overrides,
  }
}

/** The Tasks page (2.0): nothing going on, the import ran when the backend started. */
export function tasks(overrides: Partial<Tasks> = {}): Tasks {
  return {
    lock: null,
    runningHere: null,
    tasks: [{ task: 'import', lastRun: run(), unavailable: null }],
    onHold: [],
    queue: [],
    ...overrides,
  }
}
