import { render, screen, within } from '@testing-library/svelte'
import { i18n } from '../lib/i18n/i18n.svelte'
import { shortsPlayer } from '../lib/shortsPlayer.svelte'
import { videoCard } from '../test/data'
import ShortCard from './ShortCard.svelte'
import VideoCard from './VideoCard.svelte'

describe('VideoCard', () => {
  afterEach(() => i18n.setLang('en'))

  it('shows the API fields formatted, and links built from the ids', () => {
    const { container } = render(VideoCard, { video: videoCard() })

    expect(screen.getByRole('heading', { name: 'Crème brûlée : la recette' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Crème brûlée : la recette' })).toHaveAttribute(
      'href',
      '/watch?v=vid00000001',
    )
    expect(screen.getByRole('link', { name: 'Tom Cooks' })).toHaveAttribute('href', '/channel/UCtest')
    expect(screen.getByText(/74\.2K views/)).toBeInTheDocument()
    expect(screen.getByText('31:54')).toBeInTheDocument()
    // Image URLs exactly as the API gave them, never built by the front.
    const images = [...container.querySelectorAll('img')].map((img) => img.getAttribute('src'))
    expect(images).toEqual(['/art/from-the-api/vid00000001.webp', '/art/from-the-api/UCtest-thumb'])
  })

  it('shows the "Unavailable on YouTube" badge only when the API says active: false', () => {
    const { unmount } = render(VideoCard, { video: videoCard() })
    expect(screen.queryByText('Unavailable on YouTube')).not.toBeInTheDocument()
    unmount()

    render(VideoCard, { video: videoCard({ active: false }) })

    expect(screen.getByText('Unavailable on YouTube')).toHaveAttribute(
      'title',
      expect.stringContaining('archived copy'),
    )
  })

  it('says "Indisponible sur YouTube" in French', async () => {
    render(VideoCard, { video: videoCard({ active: false }) })

    i18n.setLang('fr')

    expect(await screen.findByText('Indisponible sur YouTube')).toBeInTheDocument()
  })

  it('shows a placeholder when the API has no thumbnail', () => {
    render(VideoCard, {
      video: videoCard({
        thumbnailUrl: null,
        thumbnails: [],
        channel: { id: 'c', name: 'C', thumbnailUrl: null, thumbnails: [] },
      }),
    })

    expect(screen.getByTestId('thumbnail-placeholder')).toBeInTheDocument()
    expect(screen.getByTestId('avatar-placeholder')).toBeInTheDocument()
  })

  it('formats in French when the language changes', async () => {
    render(VideoCard, { video: videoCard() })

    i18n.setLang('fr')

    expect(await screen.findByText(/74,2\sk vues/)).toBeInTheDocument()
  })

  it('renders a short without a duration overlay', () => {
    render(ShortCard, { video: videoCard({ type: 'short', title: 'Short one' }) })

    const card = screen.getByTestId('short-card')
    expect(within(card).getByText('Short one')).toBeInTheDocument()
    expect(within(card).queryByText('31:54')).not.toBeInTheDocument()
  })

  describe('short links (shorts player setting)', () => {
    afterEach(() => shortsPlayer.reload())

    it('open the vertical player by default', () => {
      render(ShortCard, { video: videoCard({ id: 'sh1', type: 'short', title: 'Short one' }) })

      expect(screen.getByRole('link', { name: 'Short one' })).toHaveAttribute('href', '/shorts/sh1')
    })

    it('open the watch page with the "normal" setting, and follow a change live', async () => {
      shortsPlayer.set('normal')
      render(ShortCard, { video: videoCard({ id: 'sh1', type: 'short', title: 'Short one' }) })

      expect(screen.getByRole('link', { name: 'Short one' })).toHaveAttribute('href', '/watch?v=sh1')
      shortsPlayer.set('vertical')
      await vi.waitFor(() =>
        expect(screen.getByRole('link', { name: 'Short one' })).toHaveAttribute('href', '/shorts/sh1'),
      )
    })

    it('apply to a short shown as a video card (search results), not to other types', () => {
      render(VideoCard, { video: videoCard({ id: 'sh2', type: 'short', title: 'As a row' }), variant: 'row' })
      render(VideoCard, { video: videoCard({ id: 'st1', type: 'stream', title: 'A stream' }) })

      expect(screen.getByRole('link', { name: 'As a row' })).toHaveAttribute('href', '/shorts/sh2')
      expect(screen.getByRole('link', { name: 'A stream' })).toHaveAttribute('href', '/watch?v=st1')
    })
  })

  describe('resized art (v1.4): srcset from the API, sizes from the layout', () => {
    const thumbnail = (container: HTMLElement) => container.querySelector('.thumbnail img') as HTMLImageElement

    it.each([
      ['grid', '(max-width: 600px) 100vw, (min-width: 2600px) calc((100vw - 200px) / 6), 400px'],
      ['row', '(max-width: 600px) 100vw, 500px'],
      ['compact', '168px'],
    ] as const)('%s cards', (variant, sizes) => {
      const { container } = render(VideoCard, { video: videoCard(), variant })

      expect(thumbnail(container)).toHaveAttribute(
        'srcset',
        '/art/from-the-api/vid00000001/small 360w, /art/from-the-api/vid00000001.webp 1280w',
      )
      expect(thumbnail(container)).toHaveAttribute('sizes', sizes)
      expect(thumbnail(container)).toHaveAttribute('src', '/art/from-the-api/vid00000001.webp')
    })

    it('gives the channel avatar its versions, sized for the avatar', () => {
      const { container } = render(VideoCard, { video: videoCard() })

      const avatar = container.querySelector('.avatar img') as HTMLImageElement
      expect(avatar).toHaveAttribute('srcset', '/art/from-the-api/UCtest-thumb 900w')
      expect(avatar).toHaveAttribute('sizes', '36px')
    })

    it('keeps the first row eager and high priority', () => {
      const { container } = render(VideoCard, { video: videoCard(), priority: true })

      expect(thumbnail(container)).toHaveAttribute('loading', 'eager')
      expect(thumbnail(container)).toHaveAttribute('fetchpriority', 'high')
      expect(thumbnail(container)).toHaveAttribute('srcset')
    })

    it('has no srcset nor sizes when the API lists no version', () => {
      const { container } = render(VideoCard, {
        video: videoCard({
          thumbnails: [],
          channel: { id: 'c', name: 'C', thumbnailUrl: '/art/c', thumbnails: [] },
        }),
      })

      expect(thumbnail(container)).not.toHaveAttribute('srcset')
      expect(thumbnail(container)).not.toHaveAttribute('sizes')
      expect(container.querySelector('.avatar img')).not.toHaveAttribute('srcset')
    })

    it('sizes short cards as short cards', () => {
      const { container } = render(ShortCard, { video: videoCard({ type: 'short' }) })

      expect(thumbnail(container)).toHaveAttribute(
        'sizes',
        '(max-width: 600px) 158vw, (min-width: 2192px) calc((100vw - 192px) * 256 / 810), 633px',
      )
      expect(thumbnail(container).getAttribute('srcset')).toContain('360w')
    })
  })
})
