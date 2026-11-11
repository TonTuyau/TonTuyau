<script lang="ts">
  // The admin's accounts (2.3.0). Who can watch: everyone who reaches TonTuyau, or the logged-in accounts only (at
  // once, saying who set it and when). A new local account (a name, its password twice, a role). The accounts, local or
  // from the directory: their role, where they're from, their last login, enabled or not; a local one gets a new role
  // or a new password; any can be disabled, enabled again, deleted (asked first). The last enabled local admin can't
  // be demoted, disabled nor deleted, nor can one's own account: the page says so instead of offering it. A change
  // ends the account's sessions within minutes. A refusal says why; a 401 ends the session.
  import Button from '../components/Button.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import HelpButton from '../components/HelpButton.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import { api, ApiError } from '../lib/api/client'
  import type { AccessSettings, Account, AccountChange, Role } from '../lib/api/types'
  import { auth } from '../lib/auth.svelte'
  import { formatAge, formatDateTime } from '../lib/format'
  import { i18n, t } from '../lib/i18n/i18n.svelte'
  import { Remote } from '../lib/remote.svelte'
  import { pageTitle } from '../lib/title.svelte'

  const roles: Role[] = ['user', 'admin']
  /** As the backend: long enough to resist guessing, short enough for a passphrase. */
  const MIN_PASSWORD = 8

  const accounts = new Remote<Account[]>()
  const access = new Remote<AccessSettings>()
  $effect(() => accounts.run((signal) => api.accounts(signal)))
  $effect(() => access.run((signal) => api.accessSettings(signal)))

  $effect(() => {
    if (accounts.error?.status === 401 || access.error?.status === 401) {
      auth.expired()
    }
  })

  // ---------------------------------------------------------------- who can watch

  let savingAccess = $state(false)
  let accessRefused = $state<string | null>(null)

  async function setAccountsOnly(accountsOnly: boolean) {
    savingAccess = true
    accessRefused = null
    try {
      access.data = await api.saveAccessSettings(accountsOnly)
      auth.accountsOnly = accountsOnly
    } catch (error) {
      accessRefused = t('accounts.refused', { reason: reasonOf(error) })
    } finally {
      savingAccess = false
    }
  }

  // ---------------------------------------------------------------- a new local account

  let username = $state('')
  let password = $state('')
  let again = $state('')
  let role = $state<Role>('user')
  let creating = $state(false)
  let formError = $state<string | null>(null)
  let created = $state<string | null>(null)

  async function oncreate(event: SubmitEvent) {
    event.preventDefault()
    created = null
    formError = passwordProblem(username.trim() === '' ? null : password, again, 'accounts.nameRequired')
    if (formError !== null) {
      return
    }
    creating = true
    try {
      const account = await api.createAccount({ username: username.trim(), password, role })
      created = account.username
      username = ''
      password = ''
      again = ''
      role = 'user'
      await accounts.refresh()
    } catch (error) {
      formError = t('accounts.refused', { reason: reasonOf(error) })
    } finally {
      creating = false
    }
  }

  /** What's wrong with a password typed twice; `password` null: the name is missing (`missing` says so). */
  function passwordProblem(
    password: string | null,
    again: string,
    missing: 'accounts.nameRequired' | null = null,
  ): string | null {
    if (password === null) {
      return missing === null ? null : t(missing)
    }
    if (password.length < MIN_PASSWORD) {
      return t('accounts.passwordShort', { min: MIN_PASSWORD })
    }
    return password === again ? null : t('accounts.passwordMismatch')
  }

  // ---------------------------------------------------------------- the accounts

  /** Why the last change was refused. */
  let refused = $state<string | null>(null)
  /** The account whose deletion is being asked, or whose new password is being typed. */
  let confirming = $state<number | null>(null)
  let resetting = $state<number | null>(null)
  let newPassword = $state('')
  let newAgain = $state('')
  let resetError = $state<string | null>(null)
  /** The account a request is going on for: its buttons wait. */
  let pending = $state<number | null>(null)

  /** The enabled local admins: the last one stays (the directory down or misconfigured, it still logs in). */
  const localAdmins = $derived(
    (accounts.data ?? []).filter((a) => a.source === 'local' && a.role === 'admin' && !a.disabled).length,
  )
  const isLastLocalAdmin = (account: Account) =>
    account.source === 'local' && account.role === 'admin' && !account.disabled && localAdmins <= 1
  const isMe = (account: Account) => account.username === auth.username

  async function change(account: Account, what: AccountChange) {
    await act(account.id, () => api.changeAccount(account.id, what))
  }

  async function onreset(event: SubmitEvent, account: Account) {
    event.preventDefault()
    resetError = passwordProblem(newPassword, newAgain)
    if (resetError !== null) {
      return
    }
    await act(account.id, () => api.changeAccount(account.id, { password: newPassword }))
    if (refused === null) {
      resetting = null
      newPassword = ''
      newAgain = ''
    }
  }

  async function act(id: number, work: () => Promise<unknown>) {
    pending = id
    refused = null
    try {
      await work()
    } catch (error) {
      refused = t('accounts.actionRefused', { reason: reasonOf(error) })
    } finally {
      pending = null
      confirming = null
      await accounts.refresh()
    }
  }

  function reasonOf(error: unknown): string {
    if (error instanceof ApiError && error.status === 401) {
      auth.expired()
    }
    return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error)
  }

  pageTitle(() => t('admin.accounts'))
  const uid = $props.id()
</script>

<div class="page">
  <header>
    <h1>{t('admin.accounts')}</h1>
  </header>

  <section class="card" aria-labelledby="{uid}-access">
    <div class="head">
      <h2 id="{uid}-access">{t('accounts.access')}</h2>
      <HelpButton zone={t('accounts.access')}>
        <p>{t('accounts.access.hint')}</p>
        <p>{t('accounts.access.keys')}</p>
      </HelpButton>
    </div>
    {#if access.status === 'error' && access.error}
      <ErrorState error={access.error} onretry={() => access.retry()} />
    {:else if access.data}
      {@const settings = access.data}
      <div class="choices" role="radiogroup" aria-labelledby="{uid}-access">
        <label class="choice">
          <input
            type="radio"
            name="{uid}-access"
            checked={!settings.accountsOnly}
            disabled={savingAccess}
            onchange={() => void setAccountsOnly(false)}
          />
          <span>
            <b>{t('accounts.access.everyone')}</b>
            <small>{t('accounts.access.everyone.what')}</small>
          </span>
        </label>
        <label class="choice">
          <input
            type="radio"
            name="{uid}-access"
            checked={settings.accountsOnly}
            disabled={savingAccess}
            onchange={() => void setAccountsOnly(true)}
          />
          <span>
            <b>{t('accounts.access.accountsOnly')}</b>
            <small>{t('accounts.access.accountsOnly.what')}</small>
          </span>
        </label>
      </div>
      {#if accessRefused}
        <p class="refused" role="alert">{accessRefused}</p>
      {/if}
      <small>
        {settings.changedAt && settings.changedBy
          ? t('accounts.changed', { date: formatDateTime(settings.changedAt, i18n.lang), by: settings.changedBy })
          : t('accounts.access.default')}
      </small>
    {/if}
  </section>

  <form class="card" onsubmit={oncreate} novalidate aria-labelledby="{uid}-new">
    <div class="head">
      <h2 id="{uid}-new">{t('accounts.new')}</h2>
      <HelpButton zone={t('accounts.new')}>
        <p>{t('accounts.new.hint')}</p>
      </HelpButton>
    </div>
    <div class="fields">
      <div class="field">
        <label for="{uid}-name">{t('accounts.username')}</label>
        <input id="{uid}-name" bind:value={username} maxlength="128" autocomplete="off" />
      </div>
      <div class="field">
        <label for="{uid}-password">{t('accounts.password')}</label>
        <input id="{uid}-password" type="password" bind:value={password} autocomplete="new-password" />
      </div>
      <div class="field">
        <label for="{uid}-again">{t('accounts.passwordAgain')}</label>
        <input id="{uid}-again" type="password" bind:value={again} autocomplete="new-password" />
      </div>
      <div class="field narrow">
        <label for="{uid}-role">{t('accounts.role')}</label>
        <select id="{uid}-role" bind:value={role}>
          {#each roles as option (option)}
            <option value={option}>{t(`accounts.role.${option}`)}</option>
          {/each}
        </select>
      </div>
      <Button type="submit" variant="primary" disabled={creating}>{t('accounts.create')}</Button>
    </div>
    <small>{t(`accounts.role.${role}.what`)}</small>
    {#if formError}
      <p class="refused" role="alert">{formError}</p>
    {/if}
    {#if created}
      <p class="done" role="status">{t('accounts.created', { name: created })}</p>
    {/if}
  </form>

  <section class="list" aria-labelledby="{uid}-list">
    <div class="head">
      <h2 id="{uid}-list">{t('accounts.title')}</h2>
      <HelpButton zone={t('accounts.title')}>
        <p>{t('accounts.title.hint')}</p>
        <p>{t('accounts.lastAdmin.hint')}</p>
      </HelpButton>
    </div>
    {#if refused}
      <p class="refused" role="alert">{refused}</p>
    {/if}

    {#if accounts.status === 'loading'}
      <Skeleton layout="page" />
    {:else if accounts.status === 'error' && accounts.error}
      <ErrorState error={accounts.error} onretry={() => accounts.retry()} />
    {:else if accounts.data}
      <div class="table-box">
        <table aria-labelledby="{uid}-list">
          <thead>
            <tr>
              <th>{t('accounts.username')}</th>
              <th>{t('accounts.role')}</th>
              <th>{t('accounts.source')}</th>
              <th>{t('accounts.state')}</th>
              <th>{t('accounts.lastLogin')}</th>
              <th><span class="visually-hidden">{t('accounts.actions')}</span></th>
            </tr>
          </thead>
          <tbody>
            {#each accounts.data as account (account.id)}
              {@const locked = isLastLocalAdmin(account) || isMe(account)}
              <tr class:off={account.disabled}>
                <td>
                  <b>{account.username}</b>
                  {#if isMe(account)}<span class="tag">{t('accounts.you')}</span>{/if}
                  {#if account.displayName}<small class="block">{account.displayName}</small>{/if}
                </td>
                <td>
                  {#if account.source === 'local'}
                    <select
                      aria-label={t('accounts.roleOf', { name: account.username })}
                      value={account.role}
                      disabled={pending !== null || locked}
                      onchange={(event) => void change(account, { role: event.currentTarget.value as Role })}
                    >
                      {#each roles as option (option)}
                        <option value={option}>{t(`accounts.role.${option}`)}</option>
                      {/each}
                    </select>
                  {:else}
                    {t(`accounts.role.${account.role}`)}
                    <small class="block">{t('accounts.roleFromGroups')}</small>
                  {/if}
                </td>
                <td>{t(`accounts.source.${account.source}`)}</td>
                <td>
                  <span class="pill" class:enabled={!account.disabled}>
                    {account.disabled ? t('accounts.disabled') : t('accounts.enabled')}
                  </span>
                </td>
                <td>
                  {account.lastLoginAt ? formatAge(account.lastLoginAt, i18n.lang) : t('accounts.never')}
                </td>
                <td>
                  <div class="actions">
                    {#if locked}
                      <small>{isMe(account) ? t('accounts.yours') : t('accounts.lastAdmin')}</small>
                    {/if}
                    {#if account.source === 'local'}
                      <button
                        type="button"
                        class="small"
                        disabled={pending !== null}
                        aria-label={t('accounts.resetOf', { name: account.username })}
                        onclick={() => {
                          resetting = resetting === account.id ? null : account.id
                          resetError = null
                        }}
                      >
                        {t('accounts.reset')}
                      </button>
                    {/if}
                    {#if !locked}
                      <button
                        type="button"
                        class="small"
                        disabled={pending !== null}
                        aria-label={t(account.disabled ? 'accounts.enableOf' : 'accounts.disableOf', {
                          name: account.username,
                        })}
                        onclick={() => void change(account, { disabled: !account.disabled })}
                      >
                        {account.disabled ? t('accounts.enable') : t('accounts.disable')}
                      </button>
                      <button
                        type="button"
                        class="small"
                        disabled={pending !== null}
                        aria-label={t('accounts.deleteOf', { name: account.username })}
                        onclick={() => (confirming = account.id)}
                      >
                        {t('accounts.delete')}
                      </button>
                    {/if}
                  </div>
                </td>
              </tr>
              {#if resetting === account.id}
                <tr class="inline">
                  <td colspan="6">
                    <form
                      class="reset"
                      novalidate
                      aria-label={t('accounts.resetOf', { name: account.username })}
                      onsubmit={(event) => void onreset(event, account)}
                    >
                      <div class="field">
                        <label for="{uid}-reset-{account.id}">{t('accounts.newPassword')}</label>
                        <input
                          id="{uid}-reset-{account.id}"
                          type="password"
                          bind:value={newPassword}
                          autocomplete="new-password"
                        />
                      </div>
                      <div class="field">
                        <label for="{uid}-reset-again-{account.id}">{t('accounts.passwordAgain')}</label>
                        <input
                          id="{uid}-reset-again-{account.id}"
                          type="password"
                          bind:value={newAgain}
                          autocomplete="new-password"
                        />
                      </div>
                      <Button type="submit" variant="primary" disabled={pending !== null}>
                        {t('accounts.setPassword')}
                      </Button>
                      <Button onclick={() => (resetting = null)}>{t('accounts.cancel')}</Button>
                    </form>
                    <small>{t('accounts.reset.hint')}</small>
                    {#if resetError}
                      <p class="refused" role="alert">{resetError}</p>
                    {/if}
                  </td>
                </tr>
              {/if}
              {#if confirming === account.id}
                <tr class="inline">
                  <td colspan="6">
                    <div class="confirm" role="group" aria-label={t('accounts.deleteOf', { name: account.username })}>
                      <p>
                        {t(account.source === 'ldap' ? 'accounts.confirmDeleteLdap' : 'accounts.confirmDelete', {
                          name: account.username,
                        })}
                      </p>
                      <Button
                        variant="primary"
                        disabled={pending !== null}
                        onclick={() => void act(account.id, () => api.deleteAccount(account.id))}
                      >
                        {t('accounts.delete')}
                      </Button>
                      <Button onclick={() => (confirming = null)}>{t('accounts.keep')}</Button>
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

  .card,
  .list {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .card {
    padding: var(--space-4) var(--space-5);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  .choices {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .choice {
    display: flex;
    align-items: flex-start;
    gap: var(--space-3);
    color: var(--color-text);
    font-size: var(--font-size-md);
  }

  .choice input {
    height: auto;
    margin-top: var(--space-1);
  }

  .choice span,
  .block {
    display: flex;
    flex-direction: column;
  }

  .fields,
  .reset {
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

  .field.narrow {
    flex: 0 1 auto;
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

  td select {
    height: var(--chip-height);
  }

  input:focus,
  select:focus {
    border-color: var(--color-focus);
    outline: none;
    box-shadow: 0 0 0 var(--border-width) var(--color-focus);
  }

  small {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .refused,
  .done {
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
  }

  .refused {
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .done {
    background: var(--color-success-bg);
    color: var(--color-success);
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

  .tag {
    margin-left: var(--space-2);
    padding: 0 var(--space-2);
    border-radius: var(--radius-pill);
    background: var(--color-surface);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
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
    align-items: center;
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

  .inline td {
    padding: var(--space-3) var(--space-4) var(--space-4);
    background: var(--color-surface);
    white-space: normal;
  }

  .inline .confirm,
  .inline .reset {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3);
  }

  .inline .reset {
    align-items: flex-end;
    margin-bottom: var(--space-2);
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-4) var(--space-4) var(--space-10);
    }
  }
</style>
