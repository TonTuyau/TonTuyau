import { linkify, type Segment } from './linkify'

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
