import { fireEvent, render, screen } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { fakeMedia } from '../test/fakeMedia'
import ShortsControls from './ShortsControls.svelte'

/** The controls over a fake short of 60 s, in a player box of their own. */
function setup(options: { playing?: boolean } = {}) {
  const media = fakeMedia({ duration: 60, playing: options.playing })
  const box = document.createElement('div')
  document.body.append(box)
  render(ShortsControls, { target: box, props: { media, box, duration: 60 } })
  const bar = screen.getByRole('slider', { name: 'Position in the video' })
  // jsdom lays nothing out: the bar is 200 px wide from x = 0.
  bar.getBoundingClientRect = () => ({ left: 0, top: 0, width: 200, height: 16 }) as DOMRect
  return { media, box, bar }
}

const controls = () => document.querySelector('.controls') as HTMLElement
const surface = () => document.querySelector('.surface') as HTMLElement

describe('ShortsControls (2.0.1)', () => {
  beforeEach(() => {
    localStorage.clear()
    HTMLElement.prototype.setPointerCapture = vi.fn()
  })
  afterEach(() => {
    vi.useRealTimers()
    document.body.innerHTML = ''
  })

  it('plays and pauses with its button; a big play button until it has started', async () => {
    const user = userEvent.setup()
    const { media } = setup()

    expect(screen.getAllByRole('button', { name: 'Play' })).toHaveLength(2)
    await user.click(screen.getAllByRole('button', { name: 'Play' })[0] as HTMLElement)

    expect(media.play).toHaveBeenCalled()
    expect(screen.getAllByRole('button', { name: 'Pause' })).toHaveLength(1)
    await user.click(screen.getByRole('button', { name: 'Pause' }))
    expect(media.pause).toHaveBeenCalled()
  })

  it('plays or pauses at a click on the video', async () => {
    const { media } = setup({ playing: true })

    await fireEvent.click(surface())
    expect(media.pause).toHaveBeenCalled()
    await fireEvent.click(surface())
    expect(media.play).toHaveBeenCalled()
  })

  it('shows its buttons while paused, hides them while it plays, and shows them again when the pointer moves', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const { media, box } = setup()

    expect(controls()).toHaveClass('shown')
    media.play()
    await vi.waitFor(() => expect(controls()).not.toHaveClass('shown'))

    await fireEvent.pointerMove(box, { pointerType: 'mouse' })
    expect(controls()).toHaveClass('shown')
    await vi.advanceTimersByTimeAsync(3000)
    expect(controls()).not.toHaveClass('shown')
  })

  it('shows its buttons for a moment after a tap', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const { media } = setup({ playing: true })

    await fireEvent.click(surface(), { pointerType: 'touch' })
    await fireEvent.click(surface(), { pointerType: 'touch' })
    expect(media.paused).toBe(false)
    expect(controls()).toHaveClass('shown')
    await vi.advanceTimersByTimeAsync(3000)
    expect(controls()).not.toHaveClass('shown')
  })

  it('goes where the bar is pressed, and follows a drag', async () => {
    const { media, bar } = setup()

    await fireEvent.pointerDown(bar, { clientX: 50, button: 0, pointerId: 1 })
    expect(media.currentTime).toBe(15)
    await fireEvent.pointerMove(bar, { clientX: 100, pointerId: 1 })
    expect(media.currentTime).toBe(30)
    await fireEvent.pointerUp(bar, { clientX: 100, pointerId: 1 })
    await fireEvent.pointerMove(bar, { clientX: 150, pointerId: 1 })
    expect(media.currentTime).toBe(30)
    expect(bar).toHaveAttribute('aria-valuetext', '0:30 / 1:00')
  })

  it('keeps a touch on the bar from being a swipe to the next short', () => {
    const { bar } = setup()
    const page = vi.fn()
    document.body.addEventListener('touchstart', page)

    bar.dispatchEvent(new Event('touchstart', { bubbles: true }))
    surface().dispatchEvent(new Event('touchstart', { bubbles: true }))

    expect(page).toHaveBeenCalledOnce()
    document.body.removeEventListener('touchstart', page)
  })

  it('mutes and sets the volume, the same as the watch page’s', async () => {
    const user = userEvent.setup()
    localStorage.setItem('tontuyau.volume', '0.4')
    const { media } = setup()
    expect(media.volume).toBe(0.4)

    await user.click(screen.getByRole('button', { name: 'Mute' }))
    expect(media.muted).toBe(true)
    expect(localStorage.getItem('tontuyau.muted')).toBe('true')
    await fireEvent.input(screen.getByRole('slider', { name: 'Volume' }), { target: { value: '0.7' } })
    expect(media.volume).toBe(0.7)
    expect(media.muted).toBe(false)
    expect(localStorage.getItem('tontuyau.volume')).toBe('0.7')
  })

  it('takes Space, ←/→ and M, but not ↑/↓ (the page’s: the short before and after), nor a key typed in a field', async () => {
    const user = userEvent.setup()
    const { media } = setup({ playing: true })
    media.currentTime = 20

    await user.keyboard(' ')
    expect(media.pause).toHaveBeenCalled()
    await user.keyboard('{ArrowRight}')
    expect(media.currentTime).toBe(25)
    await user.keyboard('{ArrowLeft}{ArrowLeft}')
    expect(media.currentTime).toBe(15)
    await user.keyboard('m')
    expect(media.muted).toBe(true)

    const down = new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true })
    window.dispatchEvent(down)
    expect(down.defaultPrevented).toBe(false)

    const field = document.createElement('input')
    document.body.append(field)
    field.focus()
    await user.keyboard('m')
    expect(media.muted).toBe(true)
  })
})
