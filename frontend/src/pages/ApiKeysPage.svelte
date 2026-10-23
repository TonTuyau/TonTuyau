<script lang="ts">
  // The admin's API keys (1.5): making one (a name, a kind; its secret shown this once, to copy), and the keys made,
  // latest first: their kind, the secret's first characters, enabled or not, when and by whom made, their last use;
  // disabling, enabling again, deleting (asked first). A refusal says why above the table. A 401 means the session
  // ended meanwhile: the shell then goes to the login page.
  import Button from '../components/Button.svelte'
  import CopyButton from '../components/CopyButton.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import HelpButton from '../components/HelpButton.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import { api, ApiError } from '../lib/api/client'
  import type { ApiKey, ApiKeyKind, CreatedApiKey } from '../lib/api/types'
  import { auth } from '../lib/auth.svelte'
  import { formatAge, formatDateTime } from '../lib/format'
  import { i18n, t } from '../lib/i18n/i18n.svelte'
  import { Remote } from '../lib/remote.svelte'
  import { pageTitle } from '../lib/title.svelte'

  const kinds: ApiKeyKind[] = ['browserExtension']

  const keys = new Remote<ApiKey[]>()
  $effect(() => keys.run((signal) => api.apiKeys(signal)))

  let name = $state('')
  let kind = $state<ApiKeyKind>('browserExtension')
  let creating = $state(false)
  /** Why the key couldn't be made (a missing name, a name taken…); cleared by the next try. */
  let formError = $state<string | null>(null)
  /** The key just made, with its secret: shown until "Done". */
  let created = $state<CreatedApiKey | null>(null)

  /** Why the last enable, disable or delete was refused. */
  let refused = $state<string | null>(null)
  /** The key whose deletion is being asked. */
  let confirming = $state<number | null>(null)
  /** The key a request is going on for: its buttons wait. */
  let pending = $state<number | null>(null)

  $effect(() => {
    if (keys.error?.status === 401) {
      auth.expired()
    }
  })

  async function oncreate(event: SubmitEvent) {
    event.preventDefault()
    formError = null
    if (name.trim() === '') {
      formError = t('apiKeys.nameRequired')
      return
    }
    creating = true
    try {
      created = await api.createApiKey(name.trim(), kind)
      name = ''
      await keys.refresh()
    } catch (error) {
      formError = t('apiKeys.refused', { reason: reasonOf(error) })
    } finally {
      creating = false
    }
  }

  async function act(id: number, work: () => Promise<unknown>) {
    pending = id
    refused = null
    try {
      await work()
    } catch (error) {
      refused = t('apiKeys.actionRefused', { reason: reasonOf(error) })
    } finally {
      pending = null
      confirming = null
      await keys.refresh()
    }
  }

  function reasonOf(error: unknown): string {
    if (error instanceof ApiError && error.status === 401) {
      auth.expired()
    }
    return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error)
  }

  pageTitle(() => t('admin.apiKeys'))
  const nameId = $props.id()
</script>

<div class="page">
  <header>
    <h1>{t('admin.apiKeys')}</h1>
  </header>

  <form class="new" onsubmit={oncreate} novalidate aria-labelledby="{nameId}-title">
    <h2 id="{nameId}-title">{t('apiKeys.new')}</h2>
    <div class="fields">
      <div class="field">
        <label for="{nameId}-name">{t('apiKeys.name')}</label>
        <input
          id="{nameId}-name"
          bind:value={name}
          placeholder={t('apiKeys.namePlaceholder')}
          maxlength="60"
          autocomplete="off"
        />
      </div>
      <div class="field">
        <label for="{nameId}-kind">{t('apiKeys.kind')}</label>
        <select id="{nameId}-kind" bind:value={kind}>
          {#each kinds as option (option)}
            <option value={option}>{t(`apiKeys.kind.${option}`)}</option>
          {/each}
        </select>
      </div>
      <Button type="submit" variant="primary" disabled={creating}>{t('apiKeys.create')}</Button>
    </div>
    <small>{t(`apiKeys.kind.${kind}.what`)}</small>
    {#if formError}
      <p class="refused" role="alert">{formError}</p>
    {/if}
  </form>

  {#if created}
    {@const secret = created.secret}
    <div class="secret" role="status">
      <p>{t('apiKeys.ready', { name: created.key.name })}</p>
      <div class="row">
        <code>{secret}</code>
        <CopyButton text={() => secret} label={t('apiKeys.copy')} />
        <Button onclick={() => (created = null)}>{t('apiKeys.done')}</Button>
      </div>
    </div>
  {/if}

  <section class="keys" aria-labelledby="keys-title">
    <div class="head">
      <h2 id="keys-title">{t('apiKeys.title')}</h2>
      <HelpButton zone={t('apiKeys.title')}>
        <p>{t('apiKeys.intro')}</p>
        <p>{t('apiKeys.hint')}</p>
      </HelpButton>
    </div>
    {#if refused}
      <p class="refused" role="alert">{refused}</p>
    {/if}

    {#if keys.status === 'loading'}
      <Skeleton layout="page" />
    {:else if keys.status === 'error' && keys.error}
      <ErrorState error={keys.error} onretry={() => keys.retry()} />
    {:else if keys.data && keys.data.length === 0}
      <p class="empty">{t('apiKeys.empty')}</p>
    {:else if keys.data}
      <div class="table-box">
        <table aria-labelledby="keys-title">
          <thead>
            <tr>
              <th>{t('apiKeys.name')}</th>
              <th>{t('apiKeys.kind')}</th>
              <th>{t('apiKeys.key')}</th>
              <th>{t('apiKeys.state')}</th>
              <th>{t('apiKeys.created')}</th>
              <th>{t('apiKeys.lastUsed')}</th>
              <th><span class="visually-hidden">{t('apiKeys.actions')}</span></th>
            </tr>
          </thead>
          <tbody>
            {#each keys.data as key (key.id)}
              <tr class:off={!key.enabled}>
                <td><b>{key.name}</b></td>
                <td>{t(`apiKeys.kind.${key.kind}`)}</td>
                <td><code>{key.prefix}…</code></td>
                <td>
                  <span class="pill" class:enabled={key.enabled}>
                    {key.enabled ? t('apiKeys.enabled') : t('apiKeys.disabled')}
                  </span>
                </td>
                <td>
                  {t('apiKeys.createdBy', { date: formatDateTime(key.createdAt, i18n.lang), by: key.createdBy })}
                </td>
                <td>{key.lastUsedAt ? formatAge(key.lastUsedAt, i18n.lang) : t('apiKeys.never')}</td>
                <td>
                  <div class="actions">
                    <button
                      type="button"
                      class="small"
                      disabled={pending !== null}
                      aria-label={t(key.enabled ? 'apiKeys.disableKey' : 'apiKeys.enableKey', { name: key.name })}
                      onclick={() =>
                        void act(key.id, () => (key.enabled ? api.disableApiKey(key.id) : api.enableApiKey(key.id)))}
                    >
                      {key.enabled ? t('apiKeys.disable') : t('apiKeys.enable')}
                    </button>
                    <button
                      type="button"
                      class="small"
                      disabled={pending !== null}
                      aria-label={t('apiKeys.deleteKey', { name: key.name })}
                      onclick={() => (confirming = key.id)}
                    >
                      {t('apiKeys.delete')}
                    </button>
                  </div>
                </td>
              </tr>
              {#if confirming === key.id}
                <tr class="confirm">
                  <td colspan="7">
                    <div role="group" aria-label={t('apiKeys.deleteKey', { name: key.name })}>
                      <p>{t('apiKeys.confirmDelete', { name: key.name })}</p>
                      <Button
                        variant="primary"
                        disabled={pending !== null}
                        onclick={() => void act(key.id, () => api.deleteApiKey(key.id))}
                      >
                        {t('apiKeys.delete')}
                      </Button>
                      <Button onclick={() => (confirming = null)}>{t('apiKeys.keep')}</Button>
                    </div>
                  </td>
                </tr>
              {/if}
            {/each}
          </tbody>
        </table>
      </div>
    {/if}
  </section>
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
    max-width: var(--admin-page-max-width);
    margin: 0 auto;
    padding: var(--space-6) var(--space-8) var(--space-12);
  }

  h1 {
    font-size: var(--font-size-stat);
    font-weight: var(--font-weight-medium);
  }

  h2 {
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-medium);
  }

  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
  }

  .new,
  .keys {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .fields {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    gap: var(--space-3);
  }

  .field {
    display: flex;
    flex: 1 1 var(--card-min-width);
    flex-direction: column;
    gap: var(--space-1);
    max-width: var(--search-max-width);
  }

  label {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  input,
  select {
    height: var(--button-height);
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-input-bg);
    color: var(--color-text);
  }

  input:focus,
  select:focus {
    border-color: var(--color-focus);
    outline: none;
    box-shadow: 0 0 0 var(--border-width) var(--color-focus);
  }

  small,
  .empty {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .refused {
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .secret {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-4);
    border-radius: var(--radius-lg);
    background: var(--color-success-bg);
  }

  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  .secret code {
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    background: var(--color-bg-elevated);
    overflow-wrap: anywhere;
  }

  .table-box {
    /* Positioned: the hidden labels (.visually-hidden, absolute) then scroll with it instead of widening the page. */
    position: relative;
    overflow-x: auto;
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
  }

  table {
    width: 100%;
    border-collapse: collapse;
  }

  th {
    padding: var(--space-3) var(--space-4);
    border-bottom: var(--border-width) solid var(--color-divider);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
    text-align: left;
    white-space: nowrap;
  }

  td {
    padding: var(--space-2) var(--space-4);
    border-bottom: var(--border-width) solid var(--color-divider);
    white-space: nowrap;
  }

  tbody tr:last-child td {
    border-bottom: none;
  }

  b {
    font-weight: var(--font-weight-medium);
  }

  tr.off td:not(:last-child) {
    color: var(--color-text-secondary);
  }

  .pill {
    display: inline-flex;
    align-items: center;
    height: var(--badge-height);
    padding: 0 var(--space-2);
    border-radius: var(--radius-pill);
    background: var(--color-surface);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
  }

  .pill.enabled {
    background: var(--color-success-bg);
    color: var(--color-success);
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    gap: var(--space-2);
  }

  .small {
    height: var(--chip-height);
    padding: 0 var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: var(--color-surface);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
  }

  .small:hover:not(:disabled) {
    background: var(--color-surface-hover);
  }

  .small:disabled {
    opacity: 0.5;
  }

  .confirm td {
    background: var(--color-surface);
    white-space: normal;
  }

  .confirm div {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  .confirm p {
    flex: 1 1 var(--card-min-width);
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-4);
    }
  }
</style>
