import { linkify, parseTimecode, type Segment } from './linkify'

const links = (text: string) =>
  linkify(text)
    .filter((segment): segment is Extract<Segment, { kind: 'link' }> => segment.kind === 'link')
    .map((segment) => segment.text)

describe('linkify', () => {
  it('splits text and http(s) URLs, keeping every character in order', () => {
    const text = 'Code: https://example.org/a?b=1&c=2#d and http://example.com.\nBye'

    expect(linkify(text)).toEqual([
      { kind: 'text', text: 'Code: ' },
      { kind: 'link', text: 'https://example.org/a?b=1&c=2#d', href: 'https://example.org/a?b=1&c=2#d' },
      { kind: 'text', text: ' and ' },
      { kind: 'link', text: 'http://example.com', href: 'http://example.com/' },
      { kind: 'text', text: '.\nBye' },
    ])
    expect(
      linkify(text)
        .map((segment) => segment.text)
        .join(''),
    ).toBe(text)
  })

  it('leaves sentence punctuation and unbalanced brackets out of the URL', () => {
    expect(links('See https://example.org/x, then https://example.org/y!')).toEqual([
      'https://example.org/x',
      'https://example.org/y',
    ])
    expect(links('(docs: https://example.org/docs)')).toEqual(['https://example.org/docs'])
    expect(links('https://example.org/wiki/Thing_(fr).')).toEqual(['https://example.org/wiki/Thing_(fr)'])
    expect(links('« https://example.org/fr »')).toEqual(['https://example.org/fr'])
  })

  it('keeps timestamps, bare domains and other schemes as plain text', () => {
    const text = '00:00 Intro\n12:30 Part two\n1:02:03 End\nwww.example.org ftp://example.org javascript:alert(1)'

    expect(linkify(text)).toEqual([{ kind: 'text', text }])
  })

  it("doesn't start a link in the middle of a word or of a non-URL", () => {
    expect(links('xhttps://example.org notmail@https://example.org')).toEqual([])
    expect(links('https://')).toEqual([])
  })

  it('stops a URL at markup characters, so markup stays text around it', () => {
    expect(linkify('<a href="https://evil.example">x</a>')).toEqual([
      { kind: 'text', text: '<a href="' },
      { kind: 'link', text: 'https://evil.example', href: 'https://evil.example/' },
      { kind: 'text', text: '">x</a>' },
    ])
  })

  it('gives an empty list for an empty text', () => {
    expect(linkify('')).toEqual([])
  })
})

describe('timecodes', () => {
  const times = (text: string, duration = 10 * 3600) =>
    linkify(text, { duration })
      .filter((segment): segment is Extract<Segment, { kind: 'time' }> => segment.kind === 'time')
      .map((segment) => [segment.text, segment.seconds])

  it.each([
    ['0:00', 0],
    ['1:23', 83],
    ['01:23', 83],
    ['12:34', 754],
    ['59:59', 3599],
    ['75:30', 4530],
    ['1:02:03', 3723],
    ['01:02:03', 3723],
    ['12:34:56', 45296],
  ])('reads %s', (text, seconds) => {
    expect(parseTimecode(text)).toBe(seconds)
  })

  it.each([
    ['3:5', 'one-digit seconds'],
    ['1:60', 'seconds past 59'],
    ['1:5:00', 'one-digit minutes after hours'],
    ['1:60:00', 'minutes past 59 after hours'],
    ['25:00:00:00', 'four parts'],
    ['16:9', 'a ratio'],
    ['4:3', 'a ratio'],
    ['123:45', 'three-digit minutes'],
    ['1:23.5', 'a decimal'],
    [':12', 'no minutes'],
    ['12:', 'no seconds'],
  ])('rejects %s (%s)', (text) => {
    expect(parseTimecode(text)).toBeNull()
    expect(times(`at ${text} here`)).toEqual([])
  })

  it('finds the timecodes of a description, keeping every character in order', () => {
    const text = '0:00 Intro\n1:23 Part one (2:05:09 the end)\nAt 12:30, then 59:59.'

    expect(times(text)).toEqual([
      ['0:00', 0],
      ['1:23', 83],
      ['2:05:09', 7509],
      ['12:30', 750],
      ['59:59', 3599],
    ])
    expect(
      linkify(text, { duration: 10 * 3600 })
        .map((segment) => segment.text)
        .join(''),
    ).toBe(text)
  })

  it('leaves timecodes as text without a duration (anything but a video description)', () => {
    expect(linkify('0:00 Intro')).toEqual([{ kind: 'text', text: '0:00 Intro' }])
  })

  it('ignores times past the video', () => {
    expect(times('0:59 1:00 1:01 1:00:00', 60)).toEqual([
      ['0:59', 59],
      ['1:00', 60],
    ])
    expect(times('0:01', 0)).toEqual([])
  })

  it("doesn't link inside URLs, words or longer numbers", () => {
    const text = 'https://example.org/t=1:23 and https://example.org/?at=12:30 v1:23 1:23am 10:30h 1:23:45:67 ab12:34'

    expect(times(text)).toEqual([])
    expect(links(text)).toEqual(['https://example.org/t=1:23', 'https://example.org/?at=12:30'])
  })

  it('links a timecode right after a URL or between brackets and dashes', () => {
    expect(times('(1:23) [2:34] 3:45-4:56 « 5:00 »')).toEqual([
      ['1:23', 83],
      ['2:34', 154],
      ['3:45', 225],
      ['4:56', 296],
      ['5:00', 300],
    ])
    expect(times('https://example.org 1:23')).toEqual([['1:23', 83]])
  })
})
