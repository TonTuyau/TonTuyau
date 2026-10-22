import { fireEvent, render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import type { Chapter } from '../lib/api/types'
import { fakeMedia } from '../test/fakeMedia'
import PlayerControls from './PlayerControls.svelte'

const chapters: Chapter[] = [
  { startSeconds: 0, endSeconds: 60, title: 'Intro' },
  { startSeconds: 60, endSeconds: 120, title: 'The ingredients' },
  { startSeconds: 120, endSeconds: 180, title: 'Tasting' },
]

/** The controls over a fake video, in a player box of their own (the box goes full screen). */
function setup(options: { duration?: number; playing?: boolean; chapters?: Chapter[]; fileDuration?: number } = {}) {
  const media = fakeMedia({ duration: options.fileDuration ?? 180, playing: options.playing })
  const box = document.createElement('div')
  document.body.append(box)
  const onsizechange = vi.fn()
  render(PlayerControls, {
    target: box,
    props: {
      media,
      box,
      chapters: options.chapters ?? chapters,
      duration: options.duration ?? 180,
      size: 'small',
      onsizechange,
    },
  })
  const bar = screen.getByRole('slider', { name: 'Position in the video' })
  // jsdom lays nothing out: the bar is 200 px wide from x = 0.
  bar.getBoundingClientRect = () => ({ left: 0, top: 0, width: 200, height: 16 }) as DOMRect
  return { media, box, bar, onsizechange }
}

const time = () => document.querySelector('.time')?.textContent?.trim()

describe('PlayerControls (2.0, C1)', () => {
  beforeEach(() => {
    localStorage.clear()
    HTMLElement.prototype.setPointerCapture = vi.fn()
  })
  afterEach(() => {
    vi.useRealTimers()
    document.body.innerHTML = ''
  })

  it('plays and pauses; a big play button until it has started', async () => {
    const user = userEvent.setup()
    const { media } = setup()

    expect(screen.getAllByRole('button', { name: 'Play' })).toHaveLength(2)
    await user.click(screen.getAllByRole('button', { name: 'Play' })[0] as HTMLElement)

    expect(media.play).toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Pause' })).toBeInTheDocument()
    expect(screen.queryAllByRole('button', { name: 'Play' })).toHaveLength(0)
    await user.click(screen.getByRole('button', { name: 'Pause' }))
    expect(media.pause).toHaveBeenCalled()
  })

  it('has no big play button for a video that started by itself before the controls came', () => {
    setup({ playing: true })

    expect(screen.getByRole('button', { name: 'Pause' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Play' })).not.toBeInTheDocument()
  })

  it('says the time and the chapter playing', async () => {
    const { media } = setup()

    expect(time()).toBe('0:00 / 3:00')
    media.currentTime = 75
    await vi.waitFor(() => expect(time()).toBe('1:15 / 3:00'))
    expect(screen.getByRole('button', { name: /The ingredients/ })).toBeInTheDocument()
  })

  it('shows the chapter and the time under the pointer on the bar, and goes where it is pressed', async () => {
    const { media, bar } = setup()

    await fireEvent.pointerMove(bar, { clientX: 100 })
    expect(document.querySelector('.tip')).toHaveTextContent('The ingredients')
    expect(document.querySelector('.tip')).toHaveTextContent('1:30')

    await fireEvent.pointerDown(bar, { clientX: 150, button: 0, pointerId: 1 })
    expect(media.currentTime).toBe(135)
    await fireEvent.pointerUp(bar, { clientX: 150, pointerId: 1 })
    await fireEvent.pointerLeave(bar)
    expect(document.querySelector('.tip')).toBeNull()
  })

  it('draws a segment per chapter, and one for what no chapter covers', () => {
    setup({ chapters: [{ startSeconds: 30, endSeconds: 120, title: 'Main' }] })

    expect(document.querySelectorAll('.segment')).toHaveLength(3)
  })

  it('lists the chapters and goes to the one chosen', async () => {
    const user = userEvent.setup()
    const { media } = setup()

    await user.click(screen.getByRole('button', { name: /Intro/ }))
    const list = document.getElementById('player-chapters') as HTMLElement
    expect(within(list).getAllByRole('button')).toHaveLength(3)
    await user.click(within(list).getByRole('button', { name: /Tasting/ }))

    expect(media.currentTime).toBe(120)
    expect(document.getElementById('player-chapters')).toBeNull()
  })

  it('changes the speed and loops from the settings menu', async () => {
    const user = userEvent.setup()
    const { media } = setup()

    await user.click(screen.getByRole('button', { name: 'Settings' }))
    const menu = document.getElementById('player-settings') as HTMLElement
    await user.click(within(menu).getByRole('button', { name: '1.5×' }))
    expect(media.playbackRate).toBe(1.5)
    expect(within(menu).getByRole('button', { name: '1.5×' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'Settings' })).toHaveTextContent('1.5×')

    await user.click(within(menu).getByRole('button', { name: /Loop/ }))
    expect(media.loop).toBe(true)
    expect(within(menu).getByRole('button', { name: /Loop/ })).toHaveAttribute('aria-pressed', 'true')
  })

  it('mutes and sets the volume, remembered for the next video', async () => {
    const user = userEvent.setup()
    const { media } = setup()

    await user.click(screen.getByRole('button', { name: 'Mute' }))
    expect(media.muted).toBe(true)
    expect(screen.getByRole('button', { name: 'Unmute' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Unmute' }))
    await fireEvent.input(screen.getByRole('slider', { name: 'Volume' }), { target: { value: '0.3' } })
    expect(media.volume).toBe(0.3)

    document.body.innerHTML = ''
    const next = setup()
    expect(next.media.volume).toBe(0.3)
    expect(next.media.muted).toBe(false)
  })

  it('goes full screen with its box, and says how to leave it', async () => {
    const user = userEvent.setup()
    const { box } = setup()
    box.requestFullscreen = vi.fn(() => {
      Object.defineProperty(document, 'fullscreenElement', { value: box, configurable: true })
      document.dispatchEvent(new Event('fullscreenchange'))
      return Promise.resolve()
    })

    await user.click(screen.getByRole('button', { name: 'Full screen' }))

    expect(box.requestFullscreen).toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Exit full screen' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Large player' })).not.toBeInTheDocument()
    Object.defineProperty(document, 'fullscreenElement', { value: null, configurable: true })
  })

  it('switches between the small and the large player', async () => {
    const user = userEvent.setup()
    const { onsizechange } = setup()

    await user.click(screen.getByRole('button', { name: 'Large player' }))

    expect(onsizechange).toHaveBeenCalledWith('large')
  })

  it('takes the keyboard: Space, ←/→ 5 s, M and F anywhere but in a field, ↑/↓ in the player only', async () => {
    const user = userEvent.setup()
    const { media, bar, box } = setup()
    box.requestFullscreen = vi.fn(() => Promise.resolve())
    const field = document.createElement('input')
    document.body.append(field)

    await user.keyboard(' ')
    expect(media.play).toHaveBeenCalledTimes(1)
    await user.keyboard('{ArrowRight}{ArrowRight}{ArrowLeft}')
    expect(media.currentTime).toBe(5)
    await user.keyboard('{ArrowUp}')
    expect(media.volume).toBe(1)
    await user.keyboard('m')
    expect(media.muted).toBe(true)
    await user.keyboard('f')
    expect(box.requestFullscreen).toHaveBeenCalled()

    bar.focus()
    await user.keyboard('m{ArrowDown}')
    expect(media.muted).toBe(false)
    expect(media.volume).toBeCloseTo(0.95)

    field.focus()
    await user.keyboard(' m')
    expect(media.pause).not.toHaveBeenCalled()
    expect(media.muted).toBe(false)
  })

  it('leaves ←/→ to the volume slider when it has the focus', async () => {
    const user = userEvent.setup()
    const { media } = setup()

    screen.getByRole('slider', { name: 'Volume' }).focus()
    await user.keyboard('{ArrowRight}m')

    expect(media.currentTime).toBe(0)
    expect(media.muted).toBe(true)
  })

  it('hides 3 s after the last move while it plays, even with the focus left on a button clicked', async () => {
    vi.useFakeTimers()
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    setup({ playing: true })
    const controls = document.querySelector('.controls') as HTMLElement

    await user.click(screen.getByRole('button', { name: 'Mute' }))
    expect(controls).toHaveClass('shown')
    await vi.advanceTimersByTimeAsync(3_100)

    expect(controls).not.toHaveClass('shown')
  })

  it('closes an open menu at a click outside the player', async () => {
    const user = userEvent.setup()
    setup()
    const outside = document.createElement('button')
    document.body.append(outside)

    await user.click(screen.getByRole('button', { name: 'Settings' }))
    expect(document.getElementById('player-settings')).not.toBeNull()
    await user.click(outside)

    expect(document.getElementById('player-settings')).toBeNull()
  })

  it('without a known length: one empty bar, and a seek forward isn’t held at the start', async () => {
    const user = userEvent.setup()
    const { media } = setup({ duration: 0, fileDuration: Number.NaN, chapters: [] })

    expect(document.querySelectorAll('.segment')).toHaveLength(1)
    expect((document.querySelector('.segment') as HTMLElement).style.width).toBe('100%')
    expect(time()).toBe('0:00 / 0:00')
    await user.keyboard('{ArrowRight}')

    expect(media.currentTime).toBe(5)
  })
})
