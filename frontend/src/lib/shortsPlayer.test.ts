import { videoCard } from '../test/data'
import { initialShortsPlayer, SHORTS_PLAYER_STORAGE_KEY, shortsPlayer, videoHref } from './shortsPlayer.svelte'

describe('shorts player preference', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    shortsPlayer.reload()
  })

  it('defaults to the vertical player', () => {
    expect(initialShortsPlayer()).toBe('vertical')
  })

  it('saves the choice and reads it back, ignoring unknown values', () => {
    shortsPlayer.set('normal')
    expect(localStorage.getItem(SHORTS_PLAYER_STORAGE_KEY)).toBe('normal')
    expect(initialShortsPlayer()).toBe('normal')

    localStorage.setItem(SHORTS_PLAYER_STORAGE_KEY, 'sideways')
    expect(initialShortsPlayer()).toBe('vertical')
  })

  it('still works when storage throws', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(initialShortsPlayer()).toBe('vertical')
    shortsPlayer.set('normal')
    expect(shortsPlayer.preference).toBe('normal')
  })

  it('opens a short (the API type) in the vertical player, or on the watch page with "normal"', () => {
    const short = videoCard({ id: 'sh1', type: 'short' })
    const video = videoCard({ id: 'v1', type: 'video' })

    expect(videoHref(short)).toBe('/shorts/sh1')
    expect(videoHref(video)).toBe('/watch?v=v1')

    shortsPlayer.set('normal')
    expect(videoHref(short)).toBe('/watch?v=sh1')
    expect(videoHref(videoCard({ id: 's2', type: 'stream' }))).toBe('/watch?v=s2')
  })
})
