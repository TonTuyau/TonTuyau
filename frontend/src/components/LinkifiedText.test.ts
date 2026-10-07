import { fireEvent, render, screen } from '@testing-library/svelte'
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

  describe('timecodes (1.1.0)', () => {
    const timecodes = (onseek?: (seconds: number) => void) => ({
      duration: 600,
      href: (seconds: number) => `/watch?v=vid&t=${seconds}`,
      onseek,
    })

    it('links each timecode to its time of the video, as an in-app link, and URLs as before', () => {
      const text = '0:00 Intro\n1:23 Part https://example.org/x?t=2:00\n9:59 End\n10:01 past the end'
      const { container } = render(LinkifiedText, { text, timecodes: timecodes() })

      expect(container.textContent).toBe(text)
      const intro = screen.getByRole('link', { name: '0:00' })
      expect(intro).toHaveAttribute('href', '/watch?v=vid&t=0')
      expect(intro).not.toHaveAttribute('target')
      expect(intro).toHaveAttribute('title', 'Play from 0:00')
      expect(screen.getByRole('link', { name: '1:23' })).toHaveAttribute('href', '/watch?v=vid&t=83')
      expect(screen.getByRole('link', { name: '9:59' })).toHaveAttribute('href', '/watch?v=vid&t=599')
      expect(screen.queryByRole('link', { name: '10:01' })).not.toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'https://example.org/x?t=2:00' })).toHaveAttribute('target', '_blank')
      expect(screen.getAllByRole('link')).toHaveLength(4)
    })

    it('shows markup around timecodes as text, never as HTML', () => {
      const text = '<b>1:23</b><img src=x onerror="alert(1)">2:34<script>alert(2)</script>'
      const { container } = render(LinkifiedText, { text, timecodes: timecodes() })

      expect(container.querySelector('b, img, script')).toBeNull()
      expect(container.textContent).toBe(text)
      expect(screen.getAllByRole('link').map((link) => link.textContent)).toEqual(['1:23', '2:34'])
    })

    it('seeks on a plain click instead of following the link; a modified click is left to the browser', async () => {
      const onseek = vi.fn()
      render(LinkifiedText, { text: 'Go to 1:23', timecodes: timecodes(onseek) })
      const link = screen.getByRole('link', { name: '1:23' })

      expect(await fireEvent.click(link)).toBe(false) // default prevented
      expect(onseek).toHaveBeenCalledWith(83)

      onseek.mockClear()
      expect(await fireEvent.click(link, { ctrlKey: true })).toBe(true)
      expect(await fireEvent.click(link, { button: 1 })).toBe(true)
      expect(onseek).not.toHaveBeenCalled()
    })

    it('says "Play from" in French too', async () => {
      const { i18n } = await import('../lib/i18n/i18n.svelte')
      i18n.setLang('fr')
      try {
        render(LinkifiedText, { text: '1:23', timecodes: timecodes() })
        expect(screen.getByRole('link', { name: '1:23' })).toHaveAttribute('title', 'Lire à partir de 1:23')
      } finally {
        i18n.setLang('en')
      }
    })
  })
})
