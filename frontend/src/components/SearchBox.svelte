<script lang="ts">
  // The top bar's search box: as-you-type suggestions from /api/search/suggest (debounced, previous request
  // aborted), ARIA combobox, keyboard: ↓ ↑ move, Enter picks or searches, Esc closes.
  // Picking a suggestion works like YouTube (QA06, M5a-4): its text goes into the box and is searched,
  // whether it's a channel or a video title.
  import { api } from '../lib/api/client'
  import { SEARCH_MAX_LENGTH, SUGGEST_MAX_LENGTH } from '../lib/api/limits'
  import type { Suggestion } from '../lib/api/types'
  import { t } from '../lib/i18n/i18n.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href } from '../lib/router/routes'
  import Icon from './Icon.svelte'

  let {
    debounceMs = 150,
    size = 8,
    autofocus = false,
    onnavigate,
  }: { debounceMs?: number; size?: number; autofocus?: boolean; onnavigate?: () => void } = $props()

  const uid = $props.id()
  const listId = `${uid}-suggestions`

  let value = $state('')
  let suggestions = $state<Suggestion[]>([])
  let open = $state(false)
  let active = $state(-1)
  let input: HTMLInputElement | undefined = $state()
  let timer: ReturnType<typeof setTimeout> | undefined
  let controller: AbortController | undefined

  // The box shows the current search when the URL is a results page.
  $effect(() => {
    if (router.route.name === 'results') {
      value = router.query.get('q') ?? ''
    }
  })

  $effect(() => {
    if (autofocus) {
      input?.focus()
    }
  })

  $effect(() => () => {
    clearTimeout(timer)
    controller?.abort()
  })

  function close() {
    open = false
    active = -1
  }

  function schedule() {
    clearTimeout(timer)
    controller?.abort()
    const q = value.trim()
    // Longer than the suggest endpoint takes (contract: 1–100): no suggestions, the search itself still works.
    if (q === '' || q.length > SUGGEST_MAX_LENGTH) {
      suggestions = []
      return
    }
    timer = setTimeout(() => void load(q), debounceMs)
  }

  async function load(q: string) {
    controller = new AbortController()
    try {
      const result = await api.suggest(q, size, controller.signal)
      suggestions = result.suggestions
      active = -1
    } catch {
      // Suggestions are a convenience: on failure (or abort) the list just stays empty.
      if (!controller.signal.aborted) {
        suggestions = []
      }
    }
  }

  function oninput() {
    open = true
    active = -1
    schedule()
  }

  function search(text: string) {
    const q = text.trim()
    if (q === '') {
      return
    }
    close()
    input?.blur()
    router.navigate(href.results({ q }))
    onnavigate?.()
  }

  function choose(suggestion: Suggestion) {
    value = suggestion.text
    search(suggestion.text)
  }

  function submit(event?: SubmitEvent) {
    event?.preventDefault()
    search(value)
  }

  function onkeydown(event: KeyboardEvent) {
    const count = suggestions.length
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault()
        open = true
        if (count > 0) {
          active = (active + 1) % count
        }
        break
      case 'ArrowUp':
        event.preventDefault()
        open = true
        if (count > 0) {
          active = active <= 0 ? count - 1 : active - 1
        }
        break
      case 'Enter': {
        const picked = open && active >= 0 ? suggestions[active] : undefined
        if (picked) {
          event.preventDefault()
          choose(picked)
        }
        break
      }
      case 'Escape':
        if (open) {
          event.preventDefault()
          close()
        }
        break
    }
  }

  function clear() {
    value = ''
    suggestions = []
    close()
    input?.focus()
  }

  function onfocusout(event: FocusEvent & { currentTarget: HTMLElement }) {
    if (!(event.relatedTarget instanceof Node) || !event.currentTarget.contains(event.relatedTarget)) {
      close()
    }
  }

  const expanded = $derived(open && suggestions.length > 0)
</script>

<form class="search" role="search" onsubmit={submit} {onfocusout}>
  <div class="field">
    <input
      bind:this={input}
      bind:value
      type="search"
      role="combobox"
      autocomplete="off"
      enterkeyhint="search"
      spellcheck="false"
      maxlength={SEARCH_MAX_LENGTH}
      aria-label={t('search.label')}
      aria-autocomplete="list"
      aria-expanded={expanded}
      aria-controls={listId}
      aria-activedescendant={expanded && active >= 0 ? `${uid}-option-${active}` : undefined}
      placeholder={t('search.placeholder')}
      {oninput}
      {onkeydown}
      onfocus={() => (open = true)}
    />
    {#if value}
      <button type="button" class="clear" aria-label={t('search.clear')} onclick={clear}>
        <Icon name="close" />
      </button>
    {/if}
  </div>
  <button type="submit" class="submit" aria-label={t('search.submit')}>
    <Icon name="search" />
  </button>

  <ul id={listId} class="suggestions" role="listbox" aria-label={t('search.suggestions')} hidden={!expanded}>
    {#each suggestions as suggestion, index (`${suggestion.kind}:${suggestion.id}`)}
      <li
        id={`${uid}-option-${index}`}
        role="option"
        aria-selected={index === active}
        class:active={index === active}
        onmousedown={(event) => {
          event.preventDefault()
          choose(suggestion)
        }}
      >
        <Icon name={suggestion.kind === 'channel' ? 'person' : 'search'} />
        <span class="text">{suggestion.text}</span>
        <span class="kind">{t(`search.kind.${suggestion.kind}`)}</span>
      </li>
    {/each}
  </ul>
</form>

<style>
  .search {
    position: relative;
    display: flex;
    align-items: center;
    width: 100%;
    max-width: var(--search-max-width);
  }

  .field {
    display: flex;
    flex: 1;
    align-items: center;
    gap: var(--space-2);
    height: var(--search-height);
    padding: 0 var(--space-1) 0 var(--space-4);
    background: var(--color-input-bg);
    border: var(--border-width) solid var(--color-border);
    border-right: none;
    border-radius: var(--radius-pill) 0 0 var(--radius-pill);
    color: var(--color-text-secondary);
  }

  .field:focus-within {
    border-color: var(--color-focus);
  }

  input {
    flex: 1;
    min-width: 0;
    height: 100%;
    border: none;
    outline: none;
    background: transparent;
    font-size: var(--font-size-md);
    color: var(--color-text);
  }

  input::-webkit-search-cancel-button {
    display: none;
  }

  .clear {
    display: grid;
    place-items: center;
    width: var(--icon-button-size);
    height: calc(var(--search-height) - var(--space-2));
    border: none;
    border-radius: var(--radius-pill);
    background: transparent;
    color: var(--color-text);
  }

  .clear:hover {
    background: var(--color-surface);
  }

  .submit {
    display: grid;
    place-items: center;
    width: calc(var(--icon-button-size) * 1.6);
    height: var(--search-height);
    border: var(--border-width) solid var(--color-border);
    border-radius: 0 var(--radius-pill) var(--radius-pill) 0;
    background: var(--color-surface);
    color: var(--color-text);
  }

  .submit:hover {
    background: var(--color-surface-hover);
  }

  .suggestions {
    position: absolute;
    top: calc(100% + var(--space-1));
    left: 0;
    right: calc(var(--icon-button-size) * 1.6);
    z-index: var(--z-menu);
    margin: 0;
    padding: var(--space-4) 0;
    list-style: none;
    background: var(--color-bg-elevated);
    border-radius: var(--radius-lg);
    box-shadow: var(--shadow-menu);
  }

  .suggestions[hidden] {
    display: none;
  }

  li {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    padding: var(--space-1) var(--space-4);
    cursor: pointer;
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  li:hover,
  li.active {
    background: var(--color-surface);
  }

  .text {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .kind {
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-regular);
    color: var(--color-text-secondary);
  }
</style>
