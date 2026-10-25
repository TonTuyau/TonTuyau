<script lang="ts">
  // The admin's LDAP directory (2.3.0 mockup, docs/PLAN_2.3.0.md): on or off; the server (its URL, StartTLS, a CA
  // certificate for a directory with its own); the service account that finds the users (its password written, never
  // shown back: left empty, the one stored stays); the accounts as Jellyfin's LDAP plugin takes them (the base DN, the
  // uid and username attributes, the users filter and the admins filter: the same values copy over); the password
  // attribute (written when the directory can't change a password itself) and the directory's page for a forgotten
  // one. A test tries the settings as typed, not saved: the connection, who the admins filter and the users filter
  // find, and with a name, what TonTuyau would make of that account (no password: the service account looks it up). The local accounts whose name the directory has too are listed: they clash, and the
  // directory's can't log in.
  import Button from '../components/Button.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import HelpButton from '../components/HelpButton.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import { api, ApiError } from '../lib/api/client'
  import type { LdapSettings, LdapSettingsChange, LdapTest, LdapTestKind } from '../lib/api/types'
  import { auth } from '../lib/auth.svelte'
  import { formatDateTime } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import type { TextKey } from '../lib/i18n/types'
  import { Remote } from '../lib/remote.svelte'
  import { pageTitle } from '../lib/title.svelte'

  const settings = new Remote<LdapSettings>()
  $effect(() => settings.run((signal) => api.ldapSettings(signal)))
  $effect(() => {
    if (settings.error?.status === 401) {
      auth.expired()
    }
  })

  /** The form: what's saved, until it's changed here. */
  let form = $state<LdapSettingsChange | null>(null)
  let bindPassword = $state('')
  $effect(() => {
    const saved = settings.data
    if (saved) {
      form = changeOf(saved)
    }
  })

  /** What the form edits of the settings: all but what only the backend says. */
  function changeOf(saved: LdapSettings): LdapSettingsChange {
    return {
      enabled: saved.enabled,
      url: saved.url,
      startTls: saved.startTls,
      caCertificate: saved.caCertificate,
      bindDn: saved.bindDn,
      baseDn: saved.baseDn,
      uidAttribute: saved.uidAttribute,
      usernameAttribute: saved.usernameAttribute,
      usersFilter: saved.usersFilter,
      adminsFilter: saved.adminsFilter,
      passwordAttribute: saved.passwordAttribute,
      passwordResetUrl: saved.passwordResetUrl,
    }
  }

  /** The fields a directory in use needs; each one missing is named. */
  const required: { key: keyof LdapSettingsChange; label: TextKey }[] = [
    { key: 'url', label: 'ldap.url' },
    { key: 'bindDn', label: 'ldap.bindDn' },
    { key: 'baseDn', label: 'ldap.baseDn' },
    { key: 'uidAttribute', label: 'ldap.uidAttribute' },
    { key: 'usernameAttribute', label: 'ldap.usernameAttribute' },
    { key: 'usersFilter', label: 'ldap.usersFilter' },
    { key: 'adminsFilter', label: 'ldap.adminsFilter' },
    { key: 'passwordAttribute', label: 'ldap.passwordAttribute' },
  ]

  let saving = $state(false)
  let problem = $state<string | null>(null)
  let saved = $state(false)

  function check(change: LdapSettingsChange): string | null {
    if (!change.enabled) {
      return null
    }
    const missing = required.filter(({ key }) => String(change[key] ?? '').trim() === '')
    if (missing.length > 0) {
      return t('ldap.missing', { fields: missing.map(({ label }) => t(label)).join(', ') })
    }
    if (!/^ldaps?:\/\/[^\s/]+$/i.test(change.url.trim())) {
      return t('ldap.urlInvalid')
    }
    const filters = [change.usersFilter, change.adminsFilter].map((filter) => filter.trim())
    if (filters.some((filter) => !filter.startsWith('(') || !filter.endsWith(')'))) {
      return t('ldap.filterInvalid')
    }
    const reset = change.passwordResetUrl?.trim() ?? ''
    return reset === '' || /^https?:\/\/\S+$/i.test(reset) ? null : t('ldap.resetUrlInvalid')
  }

  function withPassword(change: LdapSettingsChange): LdapSettingsChange {
    return bindPassword === '' ? change : { ...change, bindPassword }
  }

  async function onsave(event: SubmitEvent) {
    event.preventDefault()
    if (form === null) {
      return
    }
    saved = false
    problem = check(form)
    if (problem !== null) {
      return
    }
    saving = true
    try {
      settings.data = await api.saveLdapSettings(withPassword(form))
      bindPassword = ''
      saved = true
    } catch (error) {
      problem = t('ldap.refused', { reason: reasonOf(error) })
    } finally {
      saving = false
    }
  }

  // ---------------------------------------------------------------- the tests

  /** The tests, each with its own button and result: the connection, each filter, a name looked up. */
  const tests: LdapTestKind[] = ['connection', 'admins', 'users', 'name']

  let testUser = $state('')
  let testing = $state<LdapTestKind | null>(null)
  let results = $state<Partial<Record<LdapTestKind, LdapTest>>>({})
  let testProblems = $state<Partial<Record<LdapTestKind, string>>>({})

  async function run(kind: LdapTestKind) {
    if (form === null) {
      return
    }
    results[kind] = undefined
    const problem =
      check({ ...form, enabled: true }) ??
      (kind === 'name' && testUser.trim() === '' ? t('ldap.test.nameRequired') : null)
    testProblems[kind] = problem ?? undefined
    if (problem !== null) {
      return
    }
    testing = kind
    try {
      results[kind] = await api.testLdap({
        ...withPassword(form),
        test: kind,
        ...(kind === 'name' ? { username: testUser.trim() } : {}),
      })
    } catch (error) {
      testProblems[kind] = t('ldap.refused', { reason: reasonOf(error) })
    } finally {
      testing = null
    }
  }

  function reasonOf(error: unknown): string {
    if (error instanceof ApiError && error.status === 401) {
      auth.expired()
    }
    return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error)
  }

  pageTitle(() => t('admin.ldap'))
  const uid = $props.id()
</script>

<div class="page">
  <header>
    <h1>{t('admin.ldap')}</h1>
  </header>

  {#if settings.status === 'loading'}
    <Skeleton layout="page" />
  {:else if settings.status === 'error' && settings.error}
    <ErrorState error={settings.error} onretry={() => settings.retry()} />
  {:else if settings.data && form}
    {@const stored = settings.data}
    {#if stored.clashes.length > 0}
      <p class="warning" role="status">
        {t('ldap.clashes', { names: stored.clashes.join(', ') })}
      </p>
    {/if}

    <form class="card" onsubmit={onsave} novalidate aria-labelledby="{uid}-title">
      <div class="head">
        <h2 id="{uid}-title">{t('ldap.title')}</h2>
        <HelpButton zone={t('ldap.title')}>
          <p>{t('ldap.hint')}</p>
          <p>{t('ldap.hint.filters')}</p>
          <p>{t('ldap.hint.clash')}</p>
        </HelpButton>
      </div>

      <label class="switch">
        <input type="checkbox" bind:checked={form.enabled} />
        <span>
          <b>{t('ldap.enabled')}</b>
          <small>{t('ldap.enabled.what')}</small>
        </span>
      </label>

      <fieldset>
        <legend>{t('ldap.server')}</legend>
        <div class="field wide">
          <label for="{uid}-url">{t('ldap.url')}</label>
          <input id="{uid}-url" bind:value={form.url} placeholder="ldap://ldap.example.com:3890" autocomplete="off" />
        </div>
        <label class="switch">
          <input type="checkbox" bind:checked={form.startTls} />
          <span>
            {t('ldap.startTls')}
            <small>{t('ldap.startTls.what')}</small>
          </span>
        </label>
        <div class="field wide">
          <label for="{uid}-ca">{t('ldap.caCertificate')}</label>
          <textarea
            id="{uid}-ca"
            rows="3"
            placeholder="-----BEGIN CERTIFICATE-----"
            value={form.caCertificate ?? ''}
            oninput={(event) => {
              if (form) form.caCertificate = event.currentTarget.value.trim() === '' ? null : event.currentTarget.value
            }}></textarea>
          <small>{t('ldap.caCertificate.what')}</small>
        </div>
      </fieldset>

      <fieldset>
        <legend>{t('ldap.serviceAccount')}</legend>
        <div class="row">
          <div class="field wide">
            <label for="{uid}-bind">{t('ldap.bindDn')}</label>
            <input
              id="{uid}-bind"
              bind:value={form.bindDn}
              placeholder="uid=tontuyau,ou=people,dc=example,dc=com"
              autocomplete="off"
            />
          </div>
          <div class="field">
            <label for="{uid}-bind-password">{t('ldap.bindPassword')}</label>
            <input
              id="{uid}-bind-password"
              type="password"
              bind:value={bindPassword}
              placeholder={stored.bindPasswordSet ? t('ldap.bindPassword.kept') : ''}
              autocomplete="new-password"
            />
          </div>
        </div>
        <small>{t('ldap.serviceAccount.what')}</small>
      </fieldset>

      <fieldset>
        <legend>{t('ldap.users')}</legend>
        <div class="field wide">
          <label for="{uid}-base">{t('ldap.baseDn')}</label>
          <input id="{uid}-base" bind:value={form.baseDn} placeholder="ou=people,dc=example,dc=com" />
        </div>
        <div class="row">
          <div class="field">
            <label for="{uid}-uid-attr">{t('ldap.uidAttribute')}</label>
            <input id="{uid}-uid-attr" bind:value={form.uidAttribute} spellcheck="false" />
            <small>{t('ldap.uidAttribute.what')}</small>
          </div>
          <div class="field">
            <label for="{uid}-name-attr">{t('ldap.usernameAttribute')}</label>
            <input id="{uid}-name-attr" bind:value={form.usernameAttribute} spellcheck="false" />
            <small>{t('ldap.usernameAttribute.what')}</small>
          </div>
        </div>
        <div class="field wide">
          <label for="{uid}-users">{t('ldap.usersFilter')}</label>
          <input
            id="{uid}-users"
            bind:value={form.usersFilter}
            placeholder="(memberof=cn=tontuyau,ou=groups,dc=example,dc=com)"
            spellcheck="false"
          />
          <small>{t('ldap.usersFilter.what')}</small>
        </div>
        <div class="field wide">
          <label for="{uid}-admins">{t('ldap.adminsFilter')}</label>
          <input
            id="{uid}-admins"
            bind:value={form.adminsFilter}
            placeholder="(memberof=cn=tontuyau_admin,ou=groups,dc=example,dc=com)"
            spellcheck="false"
          />
          <small>{t('ldap.adminsFilter.what')}</small>
        </div>
      </fieldset>

      <fieldset>
        <legend>{t('ldap.passwords')}</legend>
        <div class="field">
          <label for="{uid}-password-attr">{t('ldap.passwordAttribute')}</label>
          <input id="{uid}-password-attr" bind:value={form.passwordAttribute} spellcheck="false" />
        </div>
        <small>{t('ldap.passwords.what')}</small>
        <div class="field wide">
          <label for="{uid}-reset">{t('ldap.passwordResetUrl')}</label>
          <input
            id="{uid}-reset"
            value={form.passwordResetUrl ?? ''}
            placeholder="http://ldap.example.com:17170/$username/password"
            spellcheck="false"
            oninput={(event) => {
              if (form)
                form.passwordResetUrl = event.currentTarget.value.trim() === '' ? null : event.currentTarget.value
            }}
          />
          <small>{t('ldap.passwordResetUrl.what')}</small>
        </div>
      </fieldset>

      <div class="row end">
        <Button type="submit" variant="primary" disabled={saving}>{t('ldap.save')}</Button>
      </div>
      {#if problem}
        <p class="refused" role="alert">{problem}</p>
      {/if}
      {#if saved}
        <p class="done" role="status">{t(form.enabled ? 'ldap.saved' : 'ldap.savedOff')}</p>
      {/if}
      <small>
        {stored.changedAt && stored.changedBy
          ? t('ldap.changed', { date: formatDateTime(stored.changedAt, i18n.lang), by: stored.changedBy })
          : t('ldap.never')}
      </small>
    </form>

    <section class="card" aria-labelledby="{uid}-test">
      <div class="head">
        <h2 id="{uid}-test">{t('ldap.test')}</h2>
        <HelpButton zone={t('ldap.test')}><p>{t('ldap.test.hint')}</p></HelpButton>
      </div>
      <small>{t('ldap.test.what')}</small>
      {#each tests as kind (kind)}
        {@const result = results[kind]}
        <div class="test" role="group" aria-labelledby="{uid}-test-{kind}">
          <div class="test-head">
            <span>
              <b id="{uid}-test-{kind}">{t(`ldap.test.${kind}`)}</b>
              <small>{t(`ldap.test.${kind}.what`)}</small>
            </span>
            {#if kind === 'name'}
              <form
                class="lookup"
                novalidate
                onsubmit={(event) => {
                  event.preventDefault()
                  void run('name')
                }}
              >
                <input
                  aria-label={t('ldap.test.username')}
                  placeholder={t('ldap.test.username')}
                  bind:value={testUser}
                  autocomplete="off"
                  spellcheck="false"
                />
                <Button type="submit" disabled={testing !== null}>{t('ldap.test.run')}</Button>
              </form>
            {:else}
              <Button disabled={testing !== null} onclick={() => void run(kind)}>{t('ldap.test.run')}</Button>
            {/if}
          </div>
          {#if testProblems[kind]}
            <p class="refused" role="alert">{testProblems[kind]}</p>
          {/if}
          {#if result}
            <div class="result" role="status">
              {#if !result.connected}
                <p class="refused">{t('ldap.test.notConnected', { error: result.error ?? '' })}</p>
              {:else if kind === 'connection'}
                <p class="done">{t('ldap.test.connected')}</p>
              {:else if result.found}
                {@const found = result.found}
                <p>
                  <b>{tp('ldap.test.accounts', found.total)}</b>
                </p>
                {#if found.accounts.length > 0}
                  <span class="names">
                    {#each found.accounts as account (account.username)}
                      <span class="name" class:clash={account.clash}>
                        {account.username}{account.displayName ? ` (${account.displayName})` : ''}{account.clash
                          ? ` · ${t('ldap.test.clash')}`
                          : ''}
                      </span>
                    {/each}
                    {#if found.total > found.accounts.length}
                      <small>{t('ldap.test.more', { count: found.total - found.accounts.length })}</small>
                    {/if}
                  </span>
                {/if}
              {:else if result.user}
                {@const user = result.user}
                <dl>
                  <dt>{t('ldap.test.dn')}</dt>
                  <dd><code>{user.dn ?? t('ldap.test.notFound')}</code></dd>
                  {#if user.displayName}
                    <dt>{t('myAccount.displayName')}</dt>
                    <dd>{user.displayName}</dd>
                  {/if}
                  <dt>{t('ldap.adminsFilter')}</dt>
                  <dd>{t(user.admin ? 'ldap.test.matches' : 'ldap.test.matchesNot')}</dd>
                  <dt>{t('ldap.usersFilter')}</dt>
                  <dd>{t(user.user ? 'ldap.test.matches' : 'ldap.test.matchesNot')}</dd>
                  <dt>{t('ldap.test.verdict')}</dt>
                  <dd>
                    {#if user.role}
                      <b>{t('ldap.test.role', { role: t(`accounts.role.${user.role}`) })}</b>
                    {:else}
                      <b class="no">{t('ldap.test.refusedAs', { reason: user.refused ?? '' })}</b>
                    {/if}
                  </dd>
                </dl>
              {/if}
            </div>
          {/if}
        </div>
      {/each}
    </section>
  {/if}
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

  .card {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-4) var(--space-5);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  fieldset {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    margin: 0;
    padding: var(--space-3) 0 0;
    border: none;
    border-top: var(--border-width) solid var(--color-divider);
  }

  legend {
    padding: 0 var(--space-2) 0 0;
    font-weight: var(--font-weight-medium);
  }

  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    gap: var(--space-3);
  }

  .row.end {
    justify-content: flex-start;
  }

  .field {
    display: flex;
    flex: 1 1 var(--card-min-width);
    flex-direction: column;
    gap: var(--space-1);
    max-width: var(--search-max-width);
  }

  .field.wide {
    max-width: none;
  }

  /* Stacked in a fieldset (a column), a field keeps its own height: the flex basis above is a row's width. */
  fieldset > .field {
    flex: none;
  }

  label {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .switch {
    display: flex;
    align-items: flex-start;
    gap: var(--space-3);
    color: var(--color-text);
    font-size: var(--font-size-md);
  }

  .switch input {
    height: auto;
    margin-top: var(--space-1);
  }

  .switch span {
    display: flex;
    flex-direction: column;
  }

  input,
  textarea {
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-input-bg);
    color: var(--color-text);
  }

  input {
    height: var(--button-height);
  }

  textarea {
    padding: var(--space-2) var(--space-3);
    font-size: var(--font-size-xs);
    resize: vertical;
  }

  input:focus,
  textarea:focus {
    border-color: var(--color-focus);
    outline: none;
    box-shadow: 0 0 0 var(--border-width) var(--color-focus);
  }

  small {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .refused,
  .done,
  .warning {
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

  .warning {
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
  }

  .result {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
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
    margin: 0;
    overflow-wrap: anywhere;
  }

  b {
    font-weight: var(--font-weight-medium);
  }

  .test {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding-top: var(--space-3);
    border-top: var(--border-width) solid var(--color-divider);
  }

  .test-head {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
  }

  .test-head > span {
    display: flex;
    flex-direction: column;
  }

  .lookup {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .names {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-1) var(--space-2);
    margin-top: var(--space-1);
  }

  .name {
    padding: 0 var(--space-2);
    border-radius: var(--radius-pill);
    background: var(--color-surface);
    font-size: var(--font-size-sm);
  }

  .name.clash {
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
  }

  .no {
    color: var(--color-error);
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-4) var(--space-4) var(--space-10);
    }

    dl {
      grid-template-columns: minmax(0, 1fr);
    }
  }
</style>
