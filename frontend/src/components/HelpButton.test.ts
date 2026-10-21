import { render, screen } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { createRawSnippet } from 'svelte'
import HelpButton from './HelpButton.svelte'

const text = createRawSnippet(() => ({ render: () => '<p>Reads its name and pictures.</p>' }))

describe('HelpButton', () => {
  it('opens its help in a bubble, closed at first, and closes it again with the "?"', async () => {
    const user = userEvent.setup()
    render(HelpButton, { zone: 'The channel', children: text })

    const button = screen.getByRole('button', { name: 'Help: The channel' })
    expect(button).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByText('Reads its name and pictures.')).not.toBeVisible()

    await user.click(button)
    expect(button).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByText('Reads its name and pictures.')).toBeVisible()
    expect(document.getElementById(button.getAttribute('aria-controls') ?? '')).toHaveTextContent('Reads its name')

    await user.click(button)
    expect(button).toHaveAttribute('aria-expanded', 'false')
  })

  it('closes with Esc, the focus back on the "?", and with a click outside', async () => {
    const user = userEvent.setup()
    render(HelpButton, { zone: 'The channel', children: text })
    const button = screen.getByRole('button', { name: 'Help: The channel' })

    await user.click(button)
    await user.click(screen.getByText('Reads its name and pictures.'))
    expect(button).toHaveAttribute('aria-expanded', 'true')
    await user.keyboard('{Escape}')
    expect(button).toHaveAttribute('aria-expanded', 'false')
    expect(button).toHaveFocus()

    await user.click(button)
    await user.click(document.body)
    expect(button).toHaveAttribute('aria-expanded', 'false')
  })
})
