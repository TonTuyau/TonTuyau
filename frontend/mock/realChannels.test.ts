// @vitest-environment node
// The local real-channels layer, on a throwaway folder shaped like dev-data/mockup (no real data here).
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { afterAll, describe, expect, it } from 'vitest'
import type { ChannelDetail, Page, VideoCard } from '../src/lib/api/types.ts'
import { loadDb } from './db.ts'
import { handleApi } from './handler.ts'
import { hasRealLayer, loadRealDb, realChannelArt } from './realChannels.ts'

const dir = mkdtempSync(join(tmpdir(), 'tontuyau-real-'))
afterAll(() => rmSync(dir, { recursive: true, force: true }))

const channel = (id: string, name: string, extra: object = {}) => ({
  id,
  name,
  subscribers: 1000,
  description: 'd',
  tags: ['t'],
  thumb_path: `/cache/channels/${id}_thumb.jpg`,
  banner_path: null,
  tvart_path: `/cache/channels/${id}_tvart.jpg`, // still in TubeArchivist's data; ignored since v1.1
  active: true,
  ...extra,
})

writeFileSync(
  join(dir, 'channels.json'),
  JSON.stringify([channel('UCbig', 'Big one'), channel('UCnone', 'No videos', { thumb_path: null })]),
)
writeFileSync(
  join(dir, 'channel-video-counts.json'),
  JSON.stringify([{ id: 'UCbig', counts: { video: 5358, short: 2 } }]),
)
mkdirSync(join(dir, 'channel-art'))
writeFileSync(join(dir, 'channel-art', 'UCbig_thumb.jpg'), 'jpeg')
writeFileSync(join(dir, 'channel-art', 'UCbig_tvart.jpg'), 'jpeg')

const db = loadRealDb(loadDb(), dir)
const get = <T>(path: string) => JSON.parse(handleApi(db, 'GET', new URL(path, 'http://mock.local')).body) as T

describe('real-channels layer', () => {
  it('is detected only when its files are there', () => {
    expect(hasRealLayer(dir)).toBe(true)
    expect(hasRealLayer(join(dir, 'missing'))).toBe(false)
  })

  it('maps the snake_case channels to the contract, with art URLs only when there is a picture', () => {
    const big = get<ChannelDetail>('/api/channels/UCbig')
    const none = get<ChannelDetail>('/api/channels/UCnone')

    expect(big).toMatchObject({ name: 'Big one', thumbnailUrl: '/art/channels/UCbig/thumb', bannerUrl: null })
    expect(none.thumbnailUrl).toBeNull()
    expect(big).not.toHaveProperty('tvArtUrl')
  })

  it('takes the counts from the counts file (missing type or channel = 0)', () => {
    expect(get<ChannelDetail>('/api/channels/UCbig').videoCounts).toEqual({ videos: 5358, shorts: 2, streams: 0 })
    expect(get<ChannelDetail>('/api/channels/UCnone').videoCounts).toEqual({ videos: 0, shorts: 0, streams: 0 })
  })

  it('attaches enough synthetic videos to page through, not the real number', () => {
    const list = get<Page<VideoCard>>('/api/channels/UCbig/videos?type=video')

    expect(list.items).toHaveLength(24)
    expect(list.nextCursor).not.toBeNull()
    expect(list.total).toBeLessThan(100)
    expect(list.items.every((video) => video.channel.name === 'Big one')).toBe(true)
  })

  it('keeps the edge-case videos reachable by their ids', () => {
    expect(get<VideoCard>('/api/videos/mockHevc000').channel.id).toBe('UCbig')
  })

  it('serves only existing pictures, with safe ids and kinds', () => {
    expect(realChannelArt(db, 'UCbig', 'thumb')).toBe(join(dir, 'channel-art', 'UCbig_thumb.jpg'))
    expect(realChannelArt(db, 'UCbig', 'banner')).toBeNull()
    expect(realChannelArt(db, '../etc', 'thumb')).toBeNull()
    expect(realChannelArt(db, 'UCbig', 'passwd')).toBeNull()
    expect(realChannelArt(db, 'UCbig', 'tvart')).toBeNull()
  })
})
