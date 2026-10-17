/// <reference types="node" />
// Contract conformance: the front only sends what docs/API_CONTRACT.md allows. The whole app is driven through
// its pages (every route, hand-edited URLs with unknown values, paging, the search box, the channel filter) against
// a fake backend that records every request; each request is then checked against the contract's table below,
// and the table itself is checked against the contract document, so neither can drift on its own.
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fireEvent, render, screen } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import App from '../../App.svelte'
import {
  channelCard,
  channelDetail,
  dashboard,
  importHistory,
  page,
  run,
  tasks,
  videoCard,
  videoDetail,
} from '../../test/data'
import { goTo } from '../../test/fakeApi'
import { CHANNEL_FILTER_MAX_LENGTH, SEARCH_MAX_LENGTH, SUGGEST_MAX_LENGTH } from './limits'

type Rule =
  | { kind: 'enum'; values: readonly string[] }
  | { kind: 'int'; min: number; max: number }
  | { kind: 'text'; min: number; max: number }
  | { kind: 'videoId' }
  | { kind: 'channelId' }
  | { kind: 'cursor' }

interface Endpoint {
  /** The path as the contract's endpoint table writes it. */
  doc: string
  path: RegExp
  params: Record<string, Rule>
  required?: string[]
}

const size = (max: number): Rule => ({ kind: 'int', min: 1, max })
const cursor: Rule = { kind: 'cursor' }
const type: Rule = { kind: 'enum', values: ['video', 'short', 'stream'] }
const videoSort: Rule = { kind: 'enum', values: ['latest', 'popular', 'oldest'] }
const segment = '[^/]+'

/** docs/API_CONTRACT.md v1.3, endpoint by endpoint. */
const contract: Endpoint[] = [
  { doc: '/api/home', path: /^\/api\/home$/, params: { size: size(100) } },
  {
    doc: '/api/videos',
    path: /^\/api\/videos$/,
    params: { type, sort: videoSort, exclude: { kind: 'videoId' }, cursor, size: size(100) },
  },
  { doc: '/api/videos/{id}', path: new RegExp(`^/api/videos/${segment}$`), params: {} },
  {
    doc: '/api/videos/{id}/comments',
    path: new RegExp(`^/api/videos/${segment}/comments$`),
    params: { sort: { kind: 'enum', values: ['top', 'newest'] }, cursor: { kind: 'cursor' }, size: size(100) },
  },
  {
    doc: '/api/channels',
    path: /^\/api\/channels$/,
    params: {
      q: { kind: 'text', min: 1, max: 100 },
      sort: { kind: 'enum', values: ['name', 'subscribers'] },
      cursor,
      size: size(100),
    },
  },
  { doc: '/api/channels/{id}', path: new RegExp(`^/api/channels/${segment}$`), params: {} },
  {
    doc: '/api/channels/{id}/videos',
    path: new RegExp(`^/api/channels/${segment}/videos$`),
    params: { type, sort: videoSort, exclude: { kind: 'videoId' }, cursor, size: size(100) },
  },
  {
    doc: '/api/search',
    path: /^\/api\/search$/,
    required: ['q'],
    params: {
      q: { kind: 'text', min: 1, max: 200 },
      kind: { kind: 'enum', values: ['all', 'videos', 'channels'] },
      type,
      channel: { kind: 'channelId' },
      published: { kind: 'enum', values: ['today', 'week', 'month', 'year'] },
      duration: { kind: 'enum', values: ['short', 'medium', 'long'] },
      sort: { kind: 'enum', values: ['relevance', 'latest', 'popular'] },
      cursor,
      size: size(100),
    },
  },
  {
    doc: '/api/search/suggest',
    path: /^\/api\/search\/suggest$/,
    required: ['q'],
    params: { q: { kind: 'text', min: 1, max: 100 }, size: size(20) },
  },
  { doc: '/api/auth/me', path: /^\/api\/auth\/me$/, params: {} },
  { doc: '/api/admin/dashboard', path: /^\/api\/admin\/dashboard$/, params: {} },
  {
    doc: '/api/admin/dashboard/imports',
    path: /^\/api\/admin\/dashboard\/imports$/,
    params: { period: { kind: 'enum', values: ['day', 'week', 'month', 'year', 'all'] } },
  },
  { doc: '/api/admin/tasks', path: /^\/api\/admin\/tasks$/, params: {} },
  {
    doc: '/api/admin/runs',
    path: /^\/api\/admin\/runs$/,
    params: {
      task: {
        kind: 'enum',
        values: ['incremental', 'full', 'applyDeletions', 'refreshVideo', 'refreshChannel', 'refreshChannelVideos'],
      },
      before: { kind: 'cursor' },
      size: size(100),
    },
  },
  { doc: '/api/admin/runs/{id}', path: new RegExp(`^/api/admin/runs/${segment}$`), params: {} },
  { doc: '/api/admin/youtube', path: /^\/api\/admin\/youtube$/, params: {} },
  { doc: '/api/admin/channels/{id}', path: new RegExp(`^/api/admin/channels/${segment}$`), params: {} },
  {
    doc: '/api/admin/channels/{id}/videos',
    path: new RegExp(`^/api/admin/channels/${segment}/videos$`),
    params: {
      state: { kind: 'enum', values: ['notChecked', 'onYouTube', 'retryLater', 'gone'] },
      q: { kind: 'text', min: 1, max: 100 },
      cursor: { kind: 'cursor' },
      size: size(100),
    },
  },
  {
    doc: '/api/admin/runs/{id}/log',
    path: new RegExp(`^/api/admin/runs/${segment}/log$`),
    params: { after: { kind: 'int', min: 0, max: Number.MAX_SAFE_INTEGER }, size: { kind: 'int', min: 1, max: 2000 } },
  },
]

/** With `kind=channels` the search takes no video filter and no sort (contract "filters for videos", M5a-14). */
const channelSearchOnly = ['q', 'kind', 'cursor', 'size']

const issuedCursors = new Set<string>()

/** Every way a request breaks the contract (empty = conforms). */
function violations(url: URL): string[] {
  const endpoint = contract.find((candidate) => candidate.path.test(url.pathname))
  if (!endpoint) {
    return [`${url.pathname}: not an endpoint of the contract`]
  }
  const problems: string[] = []
  const names = [...url.searchParams.keys()]
  if (new Set(names).size !== names.length) {
    problems.push('a parameter is sent twice')
  }
  for (const name of endpoint.required ?? []) {
    if (!url.searchParams.has(name)) {
      problems.push(`${name}: required`)
    }
  }
  for (const [name, value] of url.searchParams) {
    const rule = endpoint.params[name]
    if (!rule) {
      problems.push(`${name}: not a parameter of ${endpoint.doc}`)
      continue
    }
    const fail = (why: string) => problems.push(`${name}=${value}: ${why}`)
    if (value === '') fail('sent empty (leave it out)')
    else if (rule.kind === 'enum' && !rule.values.includes(value)) fail(`not one of ${rule.values.join(', ')}`)
    else if (rule.kind === 'int' && !(/^\d+$/.test(value) && +value >= rule.min && +value <= rule.max)) {
      fail(`not an integer in ${rule.min}–${rule.max}`)
    } else if (rule.kind === 'text' && (value.trim() !== value || value.length < rule.min || value.length > rule.max)) {
      fail(`not trimmed text of ${rule.min}–${rule.max} characters`)
    } else if (rule.kind === 'videoId' && !/^[A-Za-z0-9_-]{11}$/.test(value)) fail('not a video id')
    else if (rule.kind === 'channelId' && !/^UC[A-Za-z0-9_-]{22}$/.test(value)) fail('not a channel id')
    else if (rule.kind === 'cursor' && !issuedCursors.has(value)) fail('a cursor the API never sent')
  }
  if (endpoint.doc === '/api/search' && url.searchParams.get('kind') === 'channels') {
    for (const name of names.filter((name) => !channelSearchOnly.includes(name))) {
      problems.push(`${name}: not taken by kind=channels`)
    }
  }
  return problems
}

// --- A fake backend that answers every endpoint with contract-shaped data -------------------------------------
const channelId = 'UCabcdefghijklmnopqrstuv'
let serial = 0
const nextId = (prefix: string) => `${prefix}${String(++serial).padStart(11 - prefix.length, '0')}`

function pageOf<T>(url: URL, item: () => T) {
  // One more page after the first: a cursor the front must send back as is.
  const next = url.searchParams.has('cursor') ? null : `opaque~${++serial}`
  if (next) issuedCursors.add(next)
  return page([item(), item()], 40, next)
}

const card = (url: URL) => () =>
  videoCard({
    id: nextId(url.searchParams.get('type') === 'short' ? 's' : 'v'),
    type: url.searchParams.get('type') === 'short' ? 'short' : 'video',
    channel: { id: channelId, name: 'Tom Cooks', thumbnailUrl: null, thumbnails: [] },
  })
const channelItem = () => channelCard({ id: `UC${nextId('c').padEnd(22, 'x')}` })

function answer(url: URL): unknown {
  const path = url.pathname
  const params = url.searchParams
  if (path === '/api/home') {
    return {
      sections: [
        { type: 'video', items: [card(url)()] },
        { type: 'short', items: [videoCard({ id: 's0000000001', type: 'short' })] },
      ],
    }
  }
  if (path === '/api/videos') return pageOf(url, card(url))
  // Logged in, so that the admin area asks for its dashboard too.
  if (path === '/api/auth/me') return { username: 'admin' }
  if (path === '/api/admin/dashboard') return dashboard()
  if (path === '/api/admin/dashboard/imports') return importHistory()
  if (path === '/api/admin/tasks') return tasks()
  if (/^\/api\/admin\/runs\/[^/]+\/log$/.test(path)) return { lines: [], complete: true }
  const oneRun = /^\/api\/admin\/runs\/([^/]+)$/.exec(path)
  if (oneRun) return run({ id: decodeURIComponent(oneRun[1] ?? '') })
  if (path === '/api/admin/runs') {
    // The history pages after the previous page's last run (`before`): a cursor the front sends back as is.
    const id = `run-20261001T000000Z-${String(++serial).padStart(6, '0')}`
    const next = params.has('before') ? null : id
    if (next) issuedCursors.add(next)
    return { items: [run({ id })], nextCursor: next, total: 2 }
  }
  if (path === '/api/search/suggest') {
    return { suggestions: [{ text: 'Tom Cooks', kind: 'channel', id: channelId }] }
  }
  if (path === '/api/search') {
    if (params.get('kind') === 'channels') return pageOf(url, channelItem)
    if (params.get('kind') === 'videos') return pageOf(url, card(url))
    return { channels: params.has('cursor') ? [] : [channelItem()], videos: pageOf(url, card(url)) }
  }
  if (path === '/api/channels') return pageOf(url, channelItem)
  const video = /^\/api\/videos\/([^/]+)$/.exec(path)
  if (video) {
    const id = decodeURIComponent(video[1] ?? '')
    return videoDetail({
      id,
      type: id.startsWith('s') ? 'short' : 'video',
      channel: { id: channelId, name: 'Tom Cooks', thumbnailUrl: null, thumbnails: [], subscribers: 3 },
    })
  }
  if (/^\/api\/channels\/[^/]+\/videos$/.test(path)) return pageOf(url, card(url))
  const channel = /^\/api\/channels\/([^/]+)$/.exec(path)
  if (channel) {
    return channelDetail({
      id: decodeURIComponent(channel[1] ?? ''),
      videoCounts: { videos: 3, shorts: 2, streams: 1 },
    })
  }
  return undefined
}

function fakeBackend(): URL[] {
  const calls: URL[] = []
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = new URL(input instanceof Request ? input.url : String(input), 'http://localhost')
    calls.push(url)
    const body = answer(url)
    return body === undefined
      ? new Response(JSON.stringify({ title: 'Not found', status: 404 }), {
          status: 404,
          headers: { 'content-type': 'application/problem+json' },
        })
      : new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
  })
  return calls
}

const settle = async () => {
  for (let round = 0; round < 6; round++) {
    await new Promise((resolve) => setTimeout(resolve, 0))
  }
}

/** Every page of the app, with valid, default and hand-edited (unknown) values. */
const urls = [
  '/',
  '/feed/videos',
  '/feed/shorts?sort=popular',
  '/feed/streams?sort=oldest',
  '/feed/videos?sort=newest',
  '/channels',
  '/channels?q=%20tom%20&sort=subscribers',
  '/channels?sort=size',
  `/channel/${channelId}`,
  `/channel/${channelId}/shorts?sort=popular`,
  `/channel/${channelId}/streams?sort=oldest`,
  `/channel/${channelId}/videos?sort=bogus`,
  '/watch?v=v0000000042',
  '/watch',
  '/shorts/s0000000001',
  '/results?q=cr%C3%A8me',
  '/results?q=%20a%20',
  `/results?q=a&type=short&duration=long&uploaded=week&sort=popular&channel=${channelId}`,
  `/results?q=a&type=stream&duration=medium&uploaded=today&sort=latest`,
  `/results?q=a&duration=short&uploaded=year&channel=${channelId}`,
  `/results?q=a&type=channel&duration=long&uploaded=week&sort=latest&channel=${channelId}`,
  '/results?q=a&type=bogus&duration=bogus&uploaded=bogus&sort=bogus',
  '/results?q=a&sort=relevance',
  '/results',
  '/admin',
  `/admin/channels/${channelId}`,
  `/admin/channels/${channelId}?state=gone`,
  '/admin/tasks',
  '/admin/tasks?task=full',
  '/admin/runs/run-20261012T120000Z-3f9a1c',
  '/login?next=%2Fchannels',
]

describe('contract conformance (docs/API_CONTRACT.md)', () => {
  it('the table matches the contract: every endpoint, parameter and value it lists is in the document', () => {
    // Vitest runs from frontend/ (package.json); the contract lives in the repository's docs/.
    const doc = readFileSync(resolve(process.cwd(), '../docs/API_CONTRACT.md'), 'utf8')
    const everywhere = new Set(
      [...doc.matchAll(/`type`: (`\w+`(?: \| `\w+`)*)/g)].flatMap((match) => match[1]?.match(/\w+/g) ?? []),
    )
    for (const endpoint of contract) {
      const row = doc.split('\n').find((line) => line.startsWith(`| \`GET ${endpoint.doc}\` |`))
      expect(row, endpoint.doc).toBeDefined()
      const tokens = new Set([...(row ?? '').matchAll(/`([^`]+)`/g)].map((match) => match[1]))
      for (const [name, rule] of Object.entries(endpoint.params)) {
        expect(tokens, `${endpoint.doc} ${name}`).toContain(name)
        // Values are listed in the row, except the video types (listed once, with VideoCard's `type`).
        for (const value of rule.kind === 'enum' ? rule.values : []) {
          expect(tokens.has(value) || everywhere.has(value), `${endpoint.doc} ${name}=${value}`).toBe(true)
        }
      }
    }
  })

  it('the checker refuses what the contract refuses', () => {
    const check = (url: string) => violations(new URL(url, 'http://localhost'))

    expect(check('/api/videos?sort=newest')).toHaveLength(1)
    expect(check('/api/videos?publishedAfter=2026-01-01')).toHaveLength(1)
    expect(check('/api/videos?size=0')).toHaveLength(1)
    expect(check('/api/videos?cursor=made-up')).toHaveLength(1)
    expect(check('/api/videos?type=')).toHaveLength(1)
    expect(check('/api/search?kind=all')).toEqual(['q: required'])
    expect(check(`/api/search?q=${'a'.repeat(201)}`)).toHaveLength(1)
    expect(check('/api/search?q=a&kind=channels&sort=latest')).toEqual(['sort: not taken by kind=channels'])
    expect(check('/api/search?q=a&channel=nope')).toHaveLength(1)
    expect(check('/api/search/suggest?q=a&size=21')).toHaveLength(1)
    expect(check('/api/sync/status')).toHaveLength(1)
    expect(check('/api/videos?type=short&sort=latest&exclude=s0000000001&size=24')).toEqual([])
  })

  it('every request the pages make is one the contract allows, with allowed values', async () => {
    const calls = fakeBackend()
    const user = userEvent.setup()
    render(App)

    for (const url of urls) {
      goTo(url)
      await settle()
      // The next pages: the cursor goes back exactly as the API sent it.
      for (const button of screen.queryAllByRole('button', { name: 'Show more' })) {
        await fireEvent.click(button)
      }
      await settle()
    }

    // The top search box: suggestions as you type, but never for text longer than the suggest endpoint takes.
    const box = screen.getByRole('combobox', { name: 'Search' })
    await user.type(box, ' tom ')
    await new Promise((resolve) => setTimeout(resolve, 200))
    await fireEvent.input(box, { target: { value: 'x'.repeat(SUGGEST_MAX_LENGTH + 1) } })
    await new Promise((resolve) => setTimeout(resolve, 200))
    expect(box).toHaveAttribute('maxlength', String(SEARCH_MAX_LENGTH))

    // The search page's channel filter (/api/channels?q=).
    goTo('/results?q=a')
    await settle()
    await user.click(screen.getByRole('button', { name: 'Filters' }))
    await user.type(screen.getByPlaceholderText('Channel name'), 'tom')
    await new Promise((resolve) => setTimeout(resolve, 250))

    const report = calls.flatMap((url) => violations(url).map((problem) => `${url.pathname}${url.search}: ${problem}`))
    expect(report).toEqual([])

    // Every endpoint of the contract was exercised, so the check above covers them all.
    const hit = new Set(calls.map((url) => contract.find((endpoint) => endpoint.path.test(url.pathname))?.doc))
    expect([...hit].sort()).toEqual(contract.map((endpoint) => endpoint.doc).sort())
    // And the paging really went through the API's cursors.
    expect(calls.some((url) => url.searchParams.has('cursor'))).toBe(true)
    expect(calls.some((url) => url.pathname === '/api/search/suggest' && url.searchParams.get('q') === 'tom')).toBe(
      true,
    )
    expect(calls.filter((url) => (url.searchParams.get('q')?.length ?? 0) > SUGGEST_MAX_LENGTH)).toEqual([])
  }, 30_000)

  it("the text boxes can't hold more than the API takes", async () => {
    fakeBackend()
    const user = userEvent.setup()
    render(App)
    const max = (element: HTMLElement) => element.getAttribute('maxlength')

    expect(max(screen.getByRole('combobox', { name: 'Search' }))).toBe(String(SEARCH_MAX_LENGTH))
    goTo('/channels')
    await settle()
    expect(max(screen.getByRole('searchbox', { name: 'Filter channels' }))).toBe(String(CHANNEL_FILTER_MAX_LENGTH))
    goTo(`/channel/${channelId}`)
    await settle()
    expect(max(screen.getByRole('searchbox', { name: 'Search this channel' }))).toBe(String(SEARCH_MAX_LENGTH))
    goTo('/results?q=a')
    await settle()
    await user.click(screen.getByRole('button', { name: 'Filters' }))
    expect(max(screen.getByPlaceholderText('Channel name'))).toBe(String(CHANNEL_FILTER_MAX_LENGTH))
  })
})
