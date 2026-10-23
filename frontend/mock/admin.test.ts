// @vitest-environment node
// The mock's admin side (2.1.0): enough of the admin API, shaped as the contract says, to review the admin pages.
import { describe, expect, it } from 'vitest'
import type { AdminChannel, ChannelTotals, Dashboard, Page, Run, Subscription, Tasks } from '../src/lib/api/types.ts'
import { adminMockState, handleAdmin } from './admin.ts'
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
    expect(call('POST', '/api/auth/login', { username: 'admin', password: 'x' }).status).toBe(204)
    expect(call<{ username: string }>('GET', '/api/auth/me').body).toEqual({ username: 'admin' })
  })

  it('leaves the public API to the public handler', () => {
    const { call } = setup()

    expect(call('GET', '/api/videos').status).toBeUndefined()
  })

  it('counts the dashboard from the fixtures', () => {
    const { state, call } = setup()
    state.loggedIn = true

    const dashboard = call<Dashboard>('GET', '/api/admin/dashboard').body
    const channels = call<Page<ChannelTotals>>('GET', '/api/admin/dashboard/channels').body

    expect(dashboard.totals.channels).toBe(db.channels.length)
    expect(dashboard.totals.videos + dashboard.totals.shorts + dashboard.totals.streams).toBe(db.videos.length)
    expect(channels.items.reduce((sum, row) => sum + row.total, 0)).toBe(db.videos.length)
  })

  it('keeps what an action changed: a subscription, a run started', () => {
    const { state, call } = setup()
    state.loggedIn = true
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
