import { codecName, formatAge, formatBytes, formatCompact, formatDate, formatDuration, formatNumber } from './format'

// Intl output uses narrow / non-breaking spaces in French: compare with normal spaces.
const plain = (text: string) => text.replace(/[\u202f\u00a0]/g, ' ')

describe('format', () => {
  it.each([
    [0, '0:00'],
    [9, '0:09'],
    [1914, '31:54'],
    [3600, '1:00:00'],
    [6 * 3600 + 2 * 60 + 41, '6:02:41'],
  ])('formats %i seconds as %s', (seconds, expected) => {
    expect(formatDuration(seconds)).toBe(expected)
  })

  it('formats counts compactly per language, like YouTube', () => {
    expect(formatCompact(74_198, 'en')).toBe('74.2K')
    expect(plain(formatCompact(74_198, 'fr'))).toBe('74,2 k')
    expect(formatCompact(3_456_789_012, 'en')).toBe('3.5B')
    expect(formatCompact(999, 'en')).toBe('999')
  })

  it('formats full numbers per language', () => {
    expect(formatNumber(74_198, 'en')).toBe('74,198')
    expect(plain(formatNumber(74_198, 'fr'))).toBe('74 198')
  })

  it('formats the age with the largest whole unit', () => {
    const now = new Date('2026-10-02T18:00:00Z')

    expect(formatAge('2026-10-02T16:00:00Z', 'en', now)).toBe('2 hours ago')
    expect(formatAge('2026-10-02T16:00:00Z', 'fr', now)).toBe('il y a 2 heures')
    expect(formatAge('2024-04-24T15:01:48Z', 'en', now)).toBe('2 years ago')
    expect(formatAge('2026-09-20T00:00:00Z', 'en', now)).toBe('1 week ago')
    expect(formatAge('2026-10-02T17:59:30Z', 'en', now)).toBe('30 seconds ago')
  })

  it('formats dates and sizes per language', () => {
    expect(formatDate('2026-04-24T15:01:48Z', 'en')).toBe('Apr 24, 2026')
    expect(formatDate('2026-04-24T15:01:48Z', 'fr')).toBe('24 avr. 2026')
    expect(formatBytes(1_313_668_754, 'en')).toBe('1.3 GB')
    expect(plain(formatBytes(1_313_668_754, 'fr'))).toBe('1,3 Go')
  })

  it('names codecs for people, and shows unknown ones as the API gives them', () => {
    expect(codecName('av1')).toBe('AV1')
    expect(codecName('hevc')).toBe('HEVC (H.265)')
    expect(codecName('eac3')).toBe('E-AC-3 (Dolby Digital Plus)')
    expect(codecName('theora')).toBe('theora')
  })
})
