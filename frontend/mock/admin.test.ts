// @vitest-environment node
// The mock's admin side (2.1.0, 2.2.0): enough of the admin API, shaped as the contract says, to review the admin pages.
import { describe, expect, it } from 'vitest'
import type { AdminChannel, ChannelTotals, Dashboard, Page, Run, Subscription, Tasks } from '../src/lib/api/types.ts'
import { logInAs } from './accounts.ts'
import { adminMockState, handleAdmin, seedQueue } from './admin.ts'
import { loadDb } from './db.ts'

const db = loadDb()
const NOW = Date.parse('2026-10-23T08:00:00Z')

function setup() {
  const state = adminMockState(db, NOW)
  const call = <T>(method: string, path: string, body?: unknown) => {
    const answer = handleAdmin(db, state, method, new URL(path, 'http://mock.local'), body, NOW)
    return { status: answer?.status, body: answer?.body as T }
  }
  return { state, call }
}

describe('the admin mock', () => {
  it('answers 401 until logged in, as the backend, then says who is', () => {
    const { call } = setup()

    expect(call('GET', '/api/auth/me').status).toBe(401)
    expect(call('GET', '/api/admin/dashboard').status).toBe(401)
    expect(call('POST', '/api/auth/login', { username: 'admin', password: 'admin' }).status).toBe(204)
    expect(call<{ username: string }>('GET', '/api/auth/me').body).toEqual({
      username: 'admin',
      displayName: null,
      role: 'admin',
      source: 'local',
    })
  })

  it('leaves the public API to the public handler', () => {
    const { call } = setup()

    expect(call('GET', '/api/videos').status).toBeUndefined()
  })

  it('counts the dashboard from the fixtures', () => {
    const { state, call } = setup()
    logInAs(state.accounts, 'admin')

    const dashboard = call<Dashboard>('GET', '/api/admin/dashboard').body
    const channels = call<Page<ChannelTotals>>('GET', '/api/admin/dashboard/channels').body

    expect(dashboard.totals.channels).toBe(db.channels.length)
    expect(dashboard.totals.videos + dashboard.totals.shorts + dashboard.totals.streams).toBe(db.videos.length)
    expect(channels.items.reduce((sum, row) => sum + row.total, 0)).toBe(db.videos.length)
    // 2.2.0: the storage and the channels' sizes, from the fixtures' file sizes.
    const bytes = db.videos.reduce((sum, video) => sum + video.fileSize, 0)
    expect(dashboard.storage.types.reduce((sum, type) => sum + type.bytes, 0)).toBe(bytes)
    expect(channels.items.reduce((sum, row) => sum + row.size, 0)).toBe(bytes)
  })

  it('runs a queued run now: the run going on goes back in the queue, first; the queue waits only for YouTube (2.3.0)', () => {
    const { state, call } = setup()
    logInAs(state.accounts, 'admin')
    seedQueue(state, 'youTube', NOW)

    const busy = call<Tasks>('GET', '/api/admin/tasks').body
    expect(busy.queueWaitsFor).toBe('run-youTubeFullSync-held')
    expect(busy.queue.map((run) => run.position)).toEqual([1, 2])
    const last = busy.queue[1]?.id ?? ''

    expect(call<Run>('POST', `/api/admin/runs/${last}/run-now`).status).toBe(202)
    const after = call<Tasks>('GET', '/api/admin/tasks').body
    expect(after.runningHere).toBe(last)
    expect(after.queue[0]?.id).toBe('run-refreshAll-busy')
    expect(call('POST', `/api/admin/runs/${last}/run-now`).status).toBe(409)

    seedQueue(state, 'hand', NOW)
    expect(call<Tasks>('GET', '/api/admin/tasks').body.queueWaitsFor).toBeNull()
    // Resume now while another run goes on: back in the queue, first.
    expect(call<Run>('POST', '/api/admin/runs/run-youTubeFullSync-held/resume').body.state).toBe('queued')
    expect(call<Tasks>('GET', '/api/admin/tasks').body.queue[0]?.id).toBe('run-youTubeFullSync-held')
  })

  it('holds the YouTube syncs on an almost full disk, saying why as the backend does (2.2.0)', () => {
    const { state, call } = setup()
    logInAs(state.accounts, 'admin')
    state.disk = 'low'

    const sync = call<Tasks>('GET', '/api/admin/tasks').body.tasks.find((task) => task.task === 'youTubeSync')

    expect(sync?.unavailable).toBe(
      "Under 50 GB free on the videos folder's disk (40 GB): the downloads wait until space is freed (the YouTube page sets it).",
    )
  })

  it('keeps what an action changed: a subscription, a run started', () => {
    const { state, call } = setup()
    logInAs(state.accounts, 'admin')
    const id = db.channels[2]?.id as string

    expect(call<AdminChannel>('GET', `/api/admin/channels/${id}`).body.subscription).toBeNull()
    call<Subscription>('PUT', `/api/admin/channels/${id}/subscription`, { videos: true, shorts: false, streams: true })
    expect(call<AdminChannel>('GET', `/api/admin/channels/${id}`).body.subscription?.tabs).toEqual({
      videos: true,
      shorts: false,
      streams: true,
    })

    const run = call<Run>('POST', `/api/admin/tasks/refreshChannel/runs?target=${id}`).body
    expect(call<Run>('GET', `/api/admin/runs/${run.id}`).body.target).toBe(id)
    expect(call<Tasks>('GET', '/api/admin/tasks').body.tasks.length).toBeGreaterThan(0)
  })
})
