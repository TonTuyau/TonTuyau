import { render, screen } from '@testing-library/svelte'
import ExpandableText from './ExpandableText.svelte'
import LinkifiedText from './LinkifiedText.svelte'

describe('LinkifiedText', () => {
  it('turns URLs into links opening in a new tab, without opener or referrer', () => {
    render(LinkifiedText, { text: 'Code: https://example.org/code?a=1&b=2. Chapters: 03:15 Setup' })

    const link = screen.getByRole('link', { name: 'https://example.org/code?a=1&b=2' })
    expect(link).toHaveAttribute('href', 'https://example.org/code?a=1&b=2')
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
    expect(screen.getAllByRole('link')).toHaveLength(1)
  })

  it('shows markup in the data as text, never as HTML', () => {
    const text = '<b>bold?</b> <img src=x onerror="alert(1)"> https://example.org/"><script>alert(2)</script>'
    const { container } = render(LinkifiedText, { text })

    expect(container.querySelector('b, img, script')).toBeNull()
    expect(container.textContent).toBe(text)
    expect(screen.getByRole('link')).toHaveAttribute('href', 'https://example.org/')
  })

  it('links the URLs of a description and keeps its line breaks and timestamps as text', () => {
    const text = 'Line one https://example.org/one\n00:00 Intro\n12:30 Part two'
    const { container } = render(ExpandableText, { text, more: '…more', less: 'Show less' })

    const body = container.querySelector('.body')
    expect(body?.textContent).toBe(text)
    expect(screen.getByRole('link', { name: 'https://example.org/one' })).toHaveAttribute(
      'href',
      'https://example.org/one',
    )
    expect(screen.queryByRole('link', { name: /12:30/ })).not.toBeInTheDocument()
  })
})
