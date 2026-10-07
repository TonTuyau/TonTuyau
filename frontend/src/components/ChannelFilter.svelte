<script lang="ts">
  // The search page's channel filter: type a name, pick one of the channels the API matches (/api/channels?q=).
  import { api } from '../lib/api/client'
  import { CHANNEL_FILTER_MAX_LENGTH } from '../lib/api/limits'
  import type { ChannelCard } from '../lib/api/types'
  import { t } from '../lib/i18n/i18n.svelte'
  import Avatar from './Avatar.svelte'

  let {
    hrefFor,
    debounceMs = 200,
    disabled = false,
  }: { hrefFor: (channelId: string) => string; debounceMs?: number; disabled?: boolean } = $props()

  let text = $state('')
  let matches = $state<ChannelCard[] | undefined>(undefined)
  let failed = $state(false)
  let timer: ReturnType<typeof setTimeout> | undefined
  let controller: AbortController | undefined

  $effect(() => () => {
    clearTimeout(timer)
    controller?.abort()
  })

  function oninput() {
    clearTimeout(timer)
    controller?.abort()
    const q = text.trim()
    if (q === '') {
      matches = undefined
      return
    }
    timer = setTimeout(async () => {
      controller = new AbortController()
      try {
        matches = (await api.channels({ q, size: 6 }, controller.signal)).items
        failed = false
      } catch {
        if (!controller.signal.aborted) {
          matches = []
          failed = true
        }
      }
    }, debounceMs)
  }
</script>

<div class="filter">
  <label>
    <span class="visually-hidden">{t('filter.channel')}</span>
    <input
      type="search"
      bind:value={text}
      {oninput}
      {disabled}
      maxlength={CHANNEL_FILTER_MAX_LENGTH}
      placeholder={t('filter.channelPlaceholder')}
    />
  </label>
  {#if matches && !disabled}
    {#if failed}
      <p class="none" role="alert">{t('filter.channelError')}</p>
    {:else if matches.length === 0}
      <p class="none">{t('filter.channelNone')}</p>
    {:else}
      <ul>
        {#each matches as channel (channel.id)}
          <li>
            <a href={hrefFor(channel.id)}
              ><Avatar src={channel.thumbnailUrl} versions={channel.thumbnails} size="xs" />{channel.name}</a
            >
          </li>
        {/each}
      </ul>
    {/if}
  {/if}
</div>

<style>
  .filter {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  input {
    width: 100%;
    padding: var(--space-1) 0;
    border: none;
    border-bottom: var(--border-width) solid var(--color-border);
    outline: none;
    background: transparent;
    color: var(--color-text);
  }

  input:focus {
    border-bottom-color: var(--color-text);
  }

  ul {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  a {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-1) 0;
  }

  a:hover {
    color: var(--color-link);
  }

  .none {
    color: var(--color-text-secondary);
  }
</style>
