<script lang="ts">
  // /login?next=…: username, password (an eye shows it as typed, alpha.3), "Stay logged in" (30 days instead of 8
  // hours). Every refusal shows the same
  // message: nothing tells whether the name exists. Once logged in (or if already), back to `next` when it's a page
  // of this site, else home.
  import Icon from '../components/Icon.svelte'
  import Logo from '../components/Logo.svelte'
  import { auth, type LoginResult } from '../lib/auth.svelte'
  import { errorKind, errorTexts } from '../lib/errors'
  import { t } from '../lib/i18n/i18n.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href, safeNext } from '../lib/router/routes'
  import { pageTitle } from '../lib/title.svelte'

  let username = $state('')
  let password = $state('')
  let remember = $state(false)
  let passwordShown = $state(false)
  let busy = $state(false)
  let result = $state<Exclude<LoginResult, 'ok'> | null>(null)
  let usernameInput: HTMLInputElement | undefined = $state()

  const next = $derived(safeNext(router.query.get('next')))
  const message = $derived(
    result === null
      ? null
      : result === 'refused'
        ? t('login.refused')
        : result === 'tooMany'
          ? t('login.tooMany')
          : t(errorTexts[errorKind(result)].title),
  )

  pageTitle(() => t('login.title'))

  // Already logged in (another tab, the back button): nothing to do here. Only while this is still the page shown: once
  // it went to `next`, `next` reads that page's query.
  $effect(() => {
    if (auth.status === 'unknown') {
      void auth.load()
    } else if (auth.status === 'authenticated' && !busy && router.route.name === 'login') {
      router.navigate(next, { replace: true })
    }
  })

  $effect(() => {
    usernameInput?.focus()
  })

  async function onsubmit(event: SubmitEvent) {
    event.preventDefault()
    busy = true
    result = null
    const outcome = await auth.login(username, password, remember)
    busy = false
    if (outcome === 'ok') {
      router.navigate(next, { replace: true })
      return
    }
    result = outcome
    password = ''
  }
</script>

<div class="page">
  <form class="card" {onsubmit}>
    <div class="brand"><Logo /></div>
    <h1>{t('login.title')}</h1>

    {#if message}
      <p class="message" role="alert">{message}</p>
    {/if}

    <label class="field">
      <span class="label">{t('login.username')}</span>
      <input
        bind:this={usernameInput}
        bind:value={username}
        name="username"
        autocomplete="username"
        autocapitalize="none"
        spellcheck="false"
        required
        maxlength="256"
      />
    </label>
    <!-- The eye is beside the label, not in it: the field's name stays "Password". -->
    <div class="password">
      <label class="field">
        <span class="label">{t('login.password')}</span>
        <input
          bind:value={password}
          name="password"
          type={passwordShown ? 'text' : 'password'}
          autocomplete="current-password"
          autocapitalize="none"
          spellcheck="false"
          required
        />
      </label>
      <button
        type="button"
        class="reveal"
        aria-pressed={passwordShown}
        aria-label={t('login.showPassword')}
        title={t(passwordShown ? 'login.hidePassword' : 'login.showPassword')}
        onclick={() => (passwordShown = !passwordShown)}
      >
        <Icon name={passwordShown ? 'visibilityOff' : 'visibility'} />
      </button>
    </div>
    <label class="remember">
      <input type="checkbox" bind:checked={remember} name="remember" />
      <span>{t('login.remember')}</span>
    </label>

    <div class="actions">
      <a class="back" href={href.home()}>{t('login.back')}</a>
      <button type="submit" class="submit" disabled={busy}>{t('login.submit')}</button>
    </div>
  </form>
</div>

<style>
  .page {
    display: grid;
    place-items: start center;
    padding: var(--space-12) var(--space-4);
  }

  .card {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    width: min(100%, var(--login-width));
    padding: var(--space-10) var(--space-10) var(--space-8);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  .brand {
    display: flex;
    justify-content: center;
  }

  h1 {
    font-size: var(--font-size-xxl);
    font-weight: var(--font-weight-regular);
    text-align: center;
  }

  .message {
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--color-error-bg);
    color: var(--color-error);
    font-weight: var(--font-weight-medium);
  }

  .field {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  .label {
    font-weight: var(--font-weight-medium);
  }

  input:not([type='checkbox']) {
    height: var(--search-height);
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-sm);
    background: var(--color-input-bg);
    color: var(--color-text);
    font-size: var(--font-size-md);
  }

  .password {
    position: relative;
  }

  .password input {
    width: 100%;
    padding-right: calc(var(--search-height) + var(--space-1));
  }

  /* Over the field's right end, as tall as it. */
  .reveal {
    position: absolute;
    right: 0;
    bottom: 0;
    display: grid;
    place-items: center;
    width: var(--search-height);
    height: var(--search-height);
    border: none;
    border-radius: var(--radius-sm);
    background: none;
    color: var(--color-text-secondary);
  }

  .reveal:hover,
  .reveal[aria-pressed='true'] {
    color: var(--color-text);
  }

  .reveal:focus-visible {
    outline: var(--focus-ring);
  }

  input:not([type='checkbox']):focus {
    border-color: var(--color-focus);
    outline: var(--focus-ring);
    outline-offset: 0;
  }

  .remember {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  .remember input {
    width: var(--space-4);
    height: var(--space-4);
    accent-color: var(--color-brand);
  }

  .actions {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    margin-top: var(--space-4);
  }

  .back {
    color: var(--color-link);
    font-weight: var(--font-weight-medium);
  }

  .submit {
    height: var(--button-height);
    padding: 0 var(--space-6);
    border: none;
    border-radius: var(--radius-pill);
    background: var(--color-brand);
    color: var(--color-on-brand);
    font-weight: var(--font-weight-medium);
  }

  .submit:disabled {
    opacity: 0.6;
    cursor: default;
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-4) 0;
    }

    .card {
      padding: var(--space-6) var(--space-4);
      border: none;
    }
  }
</style>
