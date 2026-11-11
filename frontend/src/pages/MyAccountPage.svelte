<script lang="ts">
  // /account (2.3.0): the logged-in account's own page, from the account menu. Its name, role and where it's from; it
  // changes its password (the current one, the new one twice: its other sessions end within minutes, this one stays):
  // a local account's here, a directory one's in the directory (the service account changes it there, for everything
  // that uses the directory). Nobody logged in: the login page, then back here.
  import Button from '../components/Button.svelte'
  import { api, ApiError } from '../lib/api/client'
  import { auth } from '../lib/auth.svelte'
  import { t } from '../lib/i18n/i18n.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href } from '../lib/router/routes'
  import { pageTitle } from '../lib/title.svelte'

  const MIN_PASSWORD = 8

  $effect(() => {
    if (auth.status === 'unknown') {
      void auth.load()
    } else if (auth.status === 'anonymous' && router.route.name === 'account') {
      router.navigate(href.login(router.path), { replace: true })
    }
  })

  let current = $state('')
  let password = $state('')
  let again = $state('')
  let saving = $state(false)
  let problem = $state<string | null>(null)
  let done = $state(false)

  async function onsubmit(event: SubmitEvent) {
    event.preventDefault()
    done = false
    problem =
      current === ''
        ? t('myAccount.currentRequired')
        : password.length < MIN_PASSWORD
          ? t('accounts.passwordShort', { min: MIN_PASSWORD })
          : password === again
            ? null
            : t('accounts.passwordMismatch')
    if (problem !== null) {
      return
    }
    saving = true
    try {
      await api.changeMyPassword(current, password)
      done = true
      current = ''
      password = ''
      again = ''
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        auth.expired()
      }
      problem = t('myAccount.refused', {
        reason: error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error),
      })
    } finally {
      saving = false
    }
  }

  pageTitle(() => t('myAccount.title'))
  const uid = $props.id()
</script>

<div class="page">
  {#if auth.status === 'authenticated' && auth.username}
    <h1>{t('myAccount.title')}</h1>

    <section class="card" aria-labelledby="{uid}-who">
      <h2 id="{uid}-who" class="visually-hidden">{t('myAccount.who')}</h2>
      <dl>
        <dt>{t('accounts.username')}</dt>
        <dd><b>{auth.username}</b></dd>
        {#if auth.displayName}
          <dt>{t('myAccount.displayName')}</dt>
          <dd>{auth.displayName}</dd>
        {/if}
        <dt>{t('accounts.role')}</dt>
        <dd>
          {t(`accounts.role.${auth.role ?? 'user'}`)} <small>{t(`accounts.role.${auth.role ?? 'user'}.what`)}</small>
        </dd>
        <dt>{t('accounts.source')}</dt>
        <dd>{t(`accounts.source.${auth.source ?? 'local'}`)}</dd>
      </dl>
    </section>

    <form class="card" {onsubmit} novalidate aria-labelledby="{uid}-password">
      <h2 id="{uid}-password">{t('myAccount.password')}</h2>
      {#if auth.source === 'ldap'}
        <p class="note">{t('myAccount.ldap')}</p>
      {/if}
      <!-- The name, for the browser's password manager. -->
      <input class="visually-hidden" autocomplete="username" value={auth.username} readonly tabindex="-1" />
      <div class="field">
        <label for="{uid}-current">{t('myAccount.current')}</label>
        <input id="{uid}-current" type="password" bind:value={current} autocomplete="current-password" />
      </div>
      <div class="field">
        <label for="{uid}-new">{t('accounts.newPassword')}</label>
        <input id="{uid}-new" type="password" bind:value={password} autocomplete="new-password" />
      </div>
      <div class="field">
        <label for="{uid}-again">{t('accounts.passwordAgain')}</label>
        <input id="{uid}-again" type="password" bind:value={again} autocomplete="new-password" />
      </div>
      <div>
        <Button type="submit" variant="primary" disabled={saving}>{t('myAccount.change')}</Button>
      </div>
      {#if problem}
        <p class="refused" role="alert">{problem}</p>
      {/if}
      {#if done}
        <p class="done" role="status">{t('myAccount.changed')}</p>
      {/if}
    </form>
  {/if}
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-5);
    max-width: var(--login-width);
    margin: 0 auto;
    padding: var(--space-8) var(--space-4) var(--space-12);
  }

  h1 {
    font-size: var(--font-size-xxl);
    font-weight: var(--font-weight-regular);
  }

  h2 {
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-medium);
  }

  .card {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-5);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  dl {
    display: grid;
    grid-template-columns: max-content minmax(0, 1fr);
    gap: var(--space-2) var(--space-4);
    margin: 0;
  }

  dt {
    color: var(--color-text-secondary);
    font-size: var(--font-size-sm);
  }

  dd {
    display: flex;
    flex-direction: column;
    margin: 0;
  }

  b {
    font-weight: var(--font-weight-medium);
  }

  .field {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  label,
  small {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  input {
    height: var(--button-height);
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-input-bg);
    color: var(--color-text);
  }

  input:focus {
    border-color: var(--color-focus);
    outline: none;
    box-shadow: 0 0 0 var(--border-width) var(--color-focus);
  }

  .note,
  .refused,
  .done {
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
  }

  .note {
    background: var(--color-surface);
  }

  .refused {
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .done {
    background: var(--color-success-bg);
    color: var(--color-success);
  }
</style>
