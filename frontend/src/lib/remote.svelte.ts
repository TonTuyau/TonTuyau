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
}
