// Loading state for one API call, and for a cursor-paged list. Pages start them from an $effect so a
// parameter change (URL) restarts the request and aborts the previous one.
import { ApiError } from './api/client'
import type { Page } from './api/types'

export type Status = 'loading' | 'ready' | 'error'

function toApiError(error: unknown): ApiError {
  return error instanceof ApiError ? error : new ApiError({ title: 'Error', status: 0, detail: String(error) })
}

function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}

/** One request: `status`, then `data` or `error`. */
export class Remote<T> {
  status = $state<Status>('loading')
  data = $state<T | undefined>(undefined)
  error = $state<ApiError | undefined>(undefined)
  #load: ((signal: AbortSignal) => Promise<T>) | undefined

  /** Starts (or restarts) the request. Returns the cleanup that aborts it, for $effect. */
  run(load: (signal: AbortSignal) => Promise<T>): () => void {
    this.#load = load
    const controller = new AbortController()
    this.status = 'loading'
    this.error = undefined
    load(controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) {
          this.data = data
          this.status = 'ready'
        }
      },
      (error: unknown) => {
        if (!controller.signal.aborted && !isAbort(error)) {
          this.error = toApiError(error)
          this.status = 'error'
        }
      },
    )
    return () => controller.abort()
  }

  retry(): void {
    if (this.#load) {
      this.run(this.#load)
    }
  }

  /**
   * Asks again without showing it (a page kept up to date, 1.4): the data stays until the new one comes, and a
   * failure leaves it as it was (the next refresh tries again). Nothing before the first answer.
   */
  async refresh(): Promise<void> {
    const load = this.#load
    if (!load || this.status !== 'ready') {
      return
    }
    const data = await load(new AbortController().signal).then(
      (value) => value,
      () => undefined,
    )
    if (data !== undefined) {
      this.data = data
    }
  }
}

/** A cursor-paged list: items appended in the API's order, `total` and `nextCursor` as the API sent them. */
export class Pager<T> {
  items = $state<T[]>([])
  total = $state<number | undefined>(undefined)
  nextCursor = $state<string | null>(null)
  status = $state<Status>('loading')
  loadingMore = $state(false)
  error = $state<ApiError | undefined>(undefined)
  moreError = $state<ApiError | undefined>(undefined)
  #fetch: ((cursor: string | null, signal: AbortSignal) => Promise<Page<T>>) | undefined
  #controller: AbortController | undefined

  /** Loads the first page with this fetcher (new filters/sort = new fetcher). Returns the abort cleanup. */
  start(fetchPage: (cursor: string | null, signal: AbortSignal) => Promise<Page<T>>): () => void {
    this.#controller?.abort()
    this.#fetch = fetchPage
    const controller = new AbortController()
    this.#controller = controller
    this.status = 'loading'
    this.loadingMore = false
    this.error = undefined
    this.moreError = undefined
    fetchPage(null, controller.signal).then(
      (page) => {
        if (!controller.signal.aborted) {
          this.items = page.items
          this.total = page.total
          this.nextCursor = page.nextCursor
          this.status = 'ready'
        }
      },
      (error: unknown) => {
        if (!controller.signal.aborted && !isAbort(error)) {
          this.error = toApiError(error)
          this.status = 'error'
        }
      },
    )
    return () => controller.abort()
  }

  get hasMore(): boolean {
    return this.status === 'ready' && this.nextCursor !== null
  }

  async loadMore(): Promise<void> {
    const fetchPage = this.#fetch
    const controller = this.#controller
    const cursor = this.nextCursor
    if (!fetchPage || !controller || cursor === null || this.loadingMore || this.status !== 'ready') {
      return
    }
    this.loadingMore = true
    this.moreError = undefined
    try {
      const page = await fetchPage(cursor, controller.signal)
      if (!controller.signal.aborted) {
        this.items = [...this.items, ...page.items]
        this.total = page.total
        this.nextCursor = page.nextCursor
      }
    } catch (error) {
      if (!controller.signal.aborted && !isAbort(error)) {
        this.moreError = toApiError(error)
      }
    } finally {
      if (!controller.signal.aborted) {
        this.loadingMore = false
      }
    }
  }

  retry(): void {
    if (this.#fetch) {
      this.start(this.#fetch)
    }
  }

  /**
   * Asks for the first page again without showing it (a list kept up to date, 1.4): new items come first, the ones
   * already there are replaced by their new version (same `key`), and the pages loaded after it stay. A failure leaves
   * the list as it was.
   */
  async refreshFirst(key: (item: T) => string): Promise<void> {
    const fetchPage = this.#fetch
    const controller = this.#controller
    if (!fetchPage || !controller || this.status !== 'ready') {
      return
    }
    const page = await fetchPage(null, controller.signal).then(
      (value) => value,
      () => undefined,
    )
    if (!page || controller.signal.aborted) {
      return
    }
    const fresh = page.items.map(key)
    const rest = this.items.filter((item) => !fresh.includes(key(item)))
    const loadedMore = this.items.length > page.items.length
    this.items = [...page.items, ...(loadedMore ? rest : [])]
    this.total = page.total
    this.nextCursor = loadedMore ? this.nextCursor : page.nextCursor
  }
}
