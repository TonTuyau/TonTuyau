// The mock library: what a synced TonTuyau would hold. Fixtures are committed JSON (synthetic, anonymised),
// edited by hand or regenerated with `pnpm run mock:fixtures`. Shapes come from the contract's types.
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import type { ChannelDetail, VideoCounts, VideoDetail } from '../src/lib/api/types.ts'

/** A channel as stored: everything but `videoCounts`, which the mock "backend" counts from the videos. */
export type ChannelFixture = Omit<ChannelDetail, 'videoCounts'>

/** A video as stored: the channel by id; the mock joins it into `channel` like the backend does. */
export type VideoFixture = Omit<VideoDetail, 'channel'> & { channelId: string }

export interface MockDb {
  channels: ChannelFixture[]
  videos: VideoFixture[]
  /** Real-channels layer only: the real per-type counts (else counted from `videos`). */
  videoCounts?: Record<string, VideoCounts>
  /** Real-channels layer only: folder with `<id>_thumb|banner.jpg`. */
  channelArtDir?: string
}

export const fixturesDir = fileURLToPath(new URL('./fixtures', import.meta.url))

export function loadDb(dir: string = fixturesDir): MockDb {
  const read = <T>(name: string): T => JSON.parse(readFileSync(join(dir, name), 'utf8')) as T
  return { channels: read<ChannelFixture[]>('channels.json'), videos: read<VideoFixture[]>('videos.json') }
}

export const emptyDb: MockDb = { channels: [], videos: [] }
