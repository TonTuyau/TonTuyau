// The front's API types are the generated ones (QA10 M5-4): these checks fail to compile if a name stops matching
// the backend's OpenAPI document, or a front convenience drifts from the shape it stands for.
import type { components } from './schema.d.ts'
import type {
  ChannelCard,
  ChannelSort,
  DurationFilter,
  ImageSize,
  Page,
  ProblemDetails,
  PublishedFilter,
  SearchKind,
  SearchSort,
  VideoCard,
  VideoDetail,
  VideoSort,
  VideoType,
} from './types'
import openapi from './openapi.json'

type Schemas = components['schemas']

describe('API types generated from the OpenAPI document', () => {
  it('give the contract its names', () => {
    expectTypeOf<VideoCard>().toEqualTypeOf<Schemas['VideoCard']>()
    expectTypeOf<VideoDetail['thumbnails']>().toEqualTypeOf<ImageSize[]>()
    expectTypeOf<VideoDetail['channel']['thumbnailUrl']>().toEqualTypeOf<null | string>()
    expectTypeOf<Page<VideoCard>>().toEqualTypeOf<Schemas['PageOfVideoCard']>()
    expectTypeOf<Page<ChannelCard>>().toEqualTypeOf<Schemas['PageOfChannelCard']>()
    expectTypeOf<ProblemDetails['title']>().toEqualTypeOf<string>()
    expectTypeOf<ProblemDetails['status']>().toEqualTypeOf<number>()
  })

  it('carry the enums of the contract, shapes and parameters', () => {
    expectTypeOf<VideoType>().toEqualTypeOf<'video' | 'short' | 'stream'>()
    expectTypeOf<VideoSort>().toEqualTypeOf<'latest' | 'popular' | 'oldest'>()
    expectTypeOf<ChannelSort>().toEqualTypeOf<'name' | 'subscribers'>()
    expectTypeOf<SearchSort>().toEqualTypeOf<'relevance' | 'latest' | 'popular'>()
    expectTypeOf<SearchKind>().toEqualTypeOf<'all' | 'videos' | 'channels'>()
    expectTypeOf<DurationFilter>().toEqualTypeOf<'short' | 'medium' | 'long'>()
    expectTypeOf<PublishedFilter>().toEqualTypeOf<'today' | 'week' | 'month' | 'year'>()
  })

  it('come from a document with no server address (the same from any host)', () => {
    expect(openapi).not.toHaveProperty('servers')
    expect(openapi.info.title).toBe('TonTuyau API')
  })
})
