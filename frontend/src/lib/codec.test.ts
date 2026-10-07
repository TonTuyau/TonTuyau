import { playability } from './codec'

const hevc = 'video/mp4; codecs="hvc1.1.6.L120.90, mp4a.40.2"'

describe('playability (R2-13, M5a-16)', () => {
  it.each(['probably', 'maybe'] as const)('answers "%s" when canPlayType does', (answer) => {
    expect(playability(hevc, { canPlayType: () => answer }, undefined)).toBe(answer)
  })

  it('keeps a canPlayType "maybe" as "maybe", even if MSE says yes', () => {
    expect(playability(hevc, { canPlayType: () => 'maybe' }, { isTypeSupported: () => true })).toBe('maybe')
  })

  it('asks Media Source Extensions when canPlayType says no', () => {
    const isTypeSupported = vi.fn(() => true)

    expect(playability(hevc, { canPlayType: () => '' }, { isTypeSupported })).toBe('probably')
    expect(isTypeSupported).toHaveBeenCalledWith(hevc)
  })

  it('says no when both say no, or there is no MSE', () => {
    expect(playability(hevc, { canPlayType: () => '' }, { isTypeSupported: () => false })).toBe('no')
    expect(playability(hevc, { canPlayType: () => '' }, undefined)).toBe('no')
  })

  it('passes the exact MIME type from the API', () => {
    const canPlayType = vi.fn(() => '')

    playability(hevc, { canPlayType }, undefined)

    expect(canPlayType).toHaveBeenCalledWith(hevc)
  })

  it('treats an MSE that throws as "no"', () => {
    const throwing = {
      isTypeSupported: () => {
        throw new Error('boom')
      },
    }

    expect(playability(hevc, { canPlayType: () => '' }, throwing)).toBe('no')
  })
})
