/**
 * A <video> that plays as far as the controls can tell (jsdom plays nothing): its time, volume, speed and the events
 * the controls listen to.
 */
export function fakeMedia({ duration = 180, playing = false } = {}) {
  const media = document.createElement('video')
  let paused = !playing
  let time = 0
  let volume = 1
  let muted = false
  let rate = 1
  const fire = (name: string) => media.dispatchEvent(new Event(name))
  Object.defineProperties(media, {
    paused: { get: () => paused },
    duration: { get: () => duration },
    buffered: { get: () => ({ length: 0, start: () => 0, end: () => 0 }) },
    currentTime: {
      get: () => time,
      set: (value: number) => {
        time = value
        fire('timeupdate')
      },
    },
    volume: {
      get: () => volume,
      set: (value: number) => {
        volume = value
        fire('volumechange')
      },
    },
    muted: {
      get: () => muted,
      set: (value: boolean) => {
        muted = value
        fire('volumechange')
      },
    },
    playbackRate: {
      get: () => rate,
      set: (value: number) => {
        rate = value
        fire('ratechange')
      },
    },
  })
  media.play = vi.fn(() => {
    paused = false
    fire('play')
    return Promise.resolve()
  })
  media.pause = vi.fn(() => {
    paused = true
    fire('pause')
  })
  return media
}
