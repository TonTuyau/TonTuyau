import { channelIdOf, channelTargetOf, videoIdOf } from './youtubeTargets'

describe('videoIdOf', () => {
  it.each([
    ['k3Fq8xZ0aQw', 'k3Fq8xZ0aQw'],
    ['  k3Fq8xZ0aQw ', 'k3Fq8xZ0aQw'],
    ['https://www.youtube.com/watch?v=k3Fq8xZ0aQw', 'k3Fq8xZ0aQw'],
    ['https://www.youtube.com/watch?v=k3Fq8xZ0aQw&t=42s&list=PL1', 'k3Fq8xZ0aQw'],
    ['youtube.com/watch?v=k3Fq8xZ0aQw', 'k3Fq8xZ0aQw'],
    ['https://m.youtube.com/watch?v=k3Fq8xZ0aQw', 'k3Fq8xZ0aQw'],
    ['https://youtu.be/k3Fq8xZ0aQw?si=abc', 'k3Fq8xZ0aQw'],
    ['https://www.youtube.com/shorts/k3Fq8xZ0aQw', 'k3Fq8xZ0aQw'],
    ['https://www.youtube.com/live/k3Fq8xZ0aQw', 'k3Fq8xZ0aQw'],
  ])('reads %s', (input, id) => {
    expect(videoIdOf(input)).toBe(id)
  })

  it.each([
    '',
    'k3Fq8xZ0aQ',
    'https://evil.example/watch?v=k3Fq8xZ0aQw',
    'https://www.youtube.com/channel/UCx7Lm2QpB0vK9sTq4aZ1wYc',
    'https://www.youtube.com/watch?v=short',
    '@underthecity',
  ])('refuses %s', (input) => {
    expect(videoIdOf(input)).toBeNull()
  })
})

describe('channelIdOf', () => {
  it.each([
    ['UCx7Lm2QpB0vK9sTq4aZ1wYc', 'UCx7Lm2QpB0vK9sTq4aZ1wYc'],
    ['https://www.youtube.com/channel/UCx7Lm2QpB0vK9sTq4aZ1wYc', 'UCx7Lm2QpB0vK9sTq4aZ1wYc'],
    ['https://www.youtube.com/channel/UCx7Lm2QpB0vK9sTq4aZ1wYc/videos', 'UCx7Lm2QpB0vK9sTq4aZ1wYc'],
    ['youtube.com/channel/UCx7Lm2QpB0vK9sTq4aZ1wYc', 'UCx7Lm2QpB0vK9sTq4aZ1wYc'],
  ])('reads %s', (input, id) => {
    expect(channelIdOf(input)).toBe(id)
  })

  it.each(['', '@underthecity', 'https://www.youtube.com/@underthecity', 'k3Fq8xZ0aQw', 'UCshort'])(
    'refuses %s',
    (input) => {
      expect(channelIdOf(input)).toBeNull()
    },
  )
})

describe('channelTargetOf', () => {
  it.each([
    ['UCx7Lm2QpB0vK9sTq4aZ1wYc', 'UCx7Lm2QpB0vK9sTq4aZ1wYc'],
    ['https://www.youtube.com/channel/UCx7Lm2QpB0vK9sTq4aZ1wYc/videos', 'UCx7Lm2QpB0vK9sTq4aZ1wYc'],
    ['@underthecity', '@underthecity'],
    ['  @under.the-city_2 ', '@under.the-city_2'],
    ['https://www.youtube.com/@underthecity', '@underthecity'],
    ['m.youtube.com/@underthecity/streams', '@underthecity'],
  ])('reads %s', (input, target) => {
    expect(channelTargetOf(input)).toBe(target)
  })

  it.each(['', '@ab', 'https://www.youtube.com/watch?v=k3Fq8xZ0aQw', 'https://vimeo.com/@underthecity', 'UCshort'])(
    'refuses %s',
    (input) => {
      expect(channelTargetOf(input)).toBeNull()
    },
  )
})
