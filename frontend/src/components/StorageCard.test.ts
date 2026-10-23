import { render, screen, within } from '@testing-library/svelte'
import StorageCard, { type Storage } from './StorageCard.svelte'

const GB = 1e9

function storage(overrides: Partial<Storage> = {}): Storage {
  return {
    disk: { total: 1000 * GB, free: 412 * GB },
    types: [
      { type: 'video', bytes: 125.5 * GB, count: 118 },
      { type: 'short', bytes: 0.34 * GB, count: 41 },
      { type: 'stream', bytes: 238 * GB, count: 28 },
    ],
    data: { art: 6.1 * GB, database: 1.24 * GB, index: 3.4 * GB },
    ...overrides,
  }
}

describe('StorageCard (2.2.0 mockup)', () => {
  it("says the disk's free space, then each part with its size and its share of the disk", () => {
    render(StorageCard, { storage: storage() })

    const zone = screen.getByRole('region', { name: 'Storage' })
    expect(within(zone).getByText('412 GB free')).toBeInTheDocument()
    expect(zone).toHaveTextContent("of 1 TB, on the videos folder's disk")
    expect(zone).toHaveTextContent('Videos 118 files 125.5 GB 12.6%')
    // Shorts are tiny beside the disk: not "0%".
    expect(zone).toHaveTextContent('Shorts 41 files 340 MB < 0.1%')
    // The disk's other files: what isn't TonTuyau's nor free.
    expect(zone).toHaveTextContent('Other files on the disk 224.2 GB 22.4%')
    expect(zone).toHaveTextContent('images 6.1 GB · database 1.2 GB · search index 3.4 GB')
    expect(within(zone).queryByRole('status')).not.toBeInTheDocument()
  })

  it('warns when less than a tenth of the disk is free', () => {
    render(StorageCard, { storage: storage({ disk: { total: 1000 * GB, free: 40 * GB } }) })

    expect(screen.getByRole('status')).toHaveTextContent('Little space left on the disk')
  })

  it("shows TonTuyau's videos alone when the disk doesn't say its free space", () => {
    render(StorageCard, { storage: storage({ disk: null }) })

    const zone = screen.getByRole('region', { name: 'Storage' })
    expect(zone).toHaveTextContent("363.8 GB of videos (the videos folder's disk doesn't say its free space)")
    expect(zone).not.toHaveTextContent('Free')
    expect(zone).toHaveTextContent('Videos 118 files 125.5 GB 34.5%')
  })
})
