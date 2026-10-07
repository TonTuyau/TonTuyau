import { render } from '@testing-library/svelte'
import { videoCard } from '../test/data'
import Thumbnail from './Thumbnail.svelte'
import VideoList from './VideoList.svelte'

// Above-the-fold thumbnails load at once with high priority (Largest Contentful Paint); the rest stay lazy.
function thumbnails(container: HTMLElement): { loading: string | null; priority: string | null }[] {
  return [...container.querySelectorAll('.thumbnail img')].map((img) => ({
    loading: img.getAttribute('loading'),
    priority: img.getAttribute('fetchpriority'),
  }))
}

const videos = Array.from({ length: 6 }, (_, index) => videoCard({ id: `vid0000000${index}` }))

describe('Thumbnail loading priority', () => {
  it('is lazy by default', () => {
    const { container } = render(Thumbnail, { src: '/art/videos/a' })

    expect(thumbnails(container)).toEqual([{ loading: 'lazy', priority: 'auto' }])
  })

  it('is eager and high priority when asked', () => {
    const { container } = render(Thumbnail, { src: '/art/videos/a', priority: true })

    expect(thumbnails(container)).toEqual([{ loading: 'eager', priority: 'high' }])
  })

  it.each(['grid', 'shorts', 'rows'] as const)('makes only the first eagerCount thumbnails eager (%s)', (layout) => {
    const { container } = render(VideoList, { videos, layout, eagerCount: 4 })

    expect(thumbnails(container).map((image) => image.loading)).toEqual([
      'eager',
      'eager',
      'eager',
      'eager',
      'lazy',
      'lazy',
    ])
  })

  it('keeps every thumbnail lazy without eagerCount (lists below the fold)', () => {
    const { container } = render(VideoList, { videos })

    expect(thumbnails(container).every((image) => image.loading === 'lazy')).toBe(true)
  })
})
