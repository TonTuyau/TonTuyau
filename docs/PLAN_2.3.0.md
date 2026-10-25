# TonTuyau 2.3.0 — plan (draft)

Accounts: users besides the admins, from an LDAP directory or made by hand, and a library that only they can watch.
**Mockup first** (`work/2.3.0-mockup`), as for 2.2.0: the pages in the mockup (`/__mock`), reviewed, then the backend.

## Today

- Every account is an admin. The first start creates one from `TONTUYAU_ADMIN_USER` / `TONTUYAU_ADMIN_PASSWORD`;
  there's no page to add another, nor to change a password.
- Browsing needs no login: the pages, the API, the thumbnails (`/art`) and the files (`/media`) are open to anyone who
  reaches TonTuyau. The admin pages and the admin API need an admin's session; LaPompe uses an API key.

## Decided (2026-10-25)

1. **Two roles**: an **admin** (everything, as today) and a **user** (watches the library; no admin page nor admin
   API). The accounts there are become admins.
2. **LDAP: a users filter and an admins filter, as Jellyfin's LDAP plugin** (2026-10-25: the values of the user's
   Jellyfin copy over). An account the admins filter matches is an admin, one the users filter matches a user; anyone
   else in the directory is refused. Set on a settings page, not in the environment.
3. **The directories in mind: LLDAP (the user's) and OpenLDAP / others** (no Active Directory preset): generic fields.
4. **The same name, local and in the directory: refused.** Adding a local account whose name the directory has is
   refused; a directory login whose name is a local account's is refused (and logged).
5. **Revoked within minutes**: a session is checked again every few minutes (the account looked up, the directory
   asked again); someone removed from the group, disabled or deleted is logged out at the next check.
6. **Local accounts: the admin types the password**; each one can change it on a **"My account"** page (in 2.3.0).
7. **"Accounts only" closes everything but `/health`**: the pages show the login page, and the API, `/art`, `/media`
   and the streams answer 401 without a session. LaPompe's API key keeps working.
8. **A directory account changes its password in TonTuyau too** (My account): TonTuyau checks the current one (a
   bind as that account), then **the service account changes it** (the user's choice, as Jellyfin's plugin): the
   LDAP Password Modify operation (RFC 3062) on that account, or, on a directory without it, the password attribute
   written (`userPassword`). The new one counts for everything that uses the directory. Where the directory refuses
   the service account (LLDAP: a password manager can't change an LLDAP admin's password), TonTuyau tries again bound
   as the account itself; refused again, the page says why.
9. **A page for a forgotten password**: the directory's own (`$username` for the name), linked from the login page.

## What the mockup will show

- **Accounts** (admin, a new page of the admin menu):
  - **Who can watch**: "Everyone who reaches TonTuyau" / "Only logged-in accounts" (the default stays everyone).
  - **The accounts**: name, role, where it's from (local / directory), last login, disabled or not. Add a local
    account (name, password twice, role); for a local one: change its role, set a new password, disable, delete; for
    a directory one: disable or delete (its role is the directory's; deleted, it's back at its next login).
  - The last enabled local admin can't be demoted, disabled nor deleted: with the directory down or misconfigured, it
    still logs in.
- **LDAP** (admin, a section of the same page or its own, to see in the mockup):
  - On or off; the server (`ldap://` or `ldaps://` host and port), StartTLS, a CA certificate (PEM) for a homelab's
    own, the service account (bind DN, password: written, never shown back).
  - The accounts, as Jellyfin's plugin: the base DN, the UID attribute (`uid`, the name typed at login), the username
    attribute (`cn`, the name shown), the users filter and the admins filter
    (`(memberof=cn=tontuyau_admin,ou=groups,dc=example,dc=com)`); the password attribute (`userPassword`) and the
    password reset URL (`http://…/$username/password`).
  - **Tests**, each with its own button and result (the settings as typed, not saved; never a password): **the
    connection** (the server answers, the service account logs in); **the admins filter** and **the users filter**
    (who each finds: how many, the first 100 by name, a name a local account has too marked: it can't log in); **a
    name** looked up by the service account (the DN, whether each filter matches, the role it'd get, or why it's
    refused). `POST /api/admin/settings/ldap/test` takes `test` = `connection` | `admins` | `users` | `name`.
  - The local accounts whose name the directory also has, listed: they clash (decision 4).
- **My account** (everyone logged in, from the account menu): the name, the role, where it's from, and its password
  changed (the current one, the new one twice): a local one's here, a directory one's in the directory (decision 8).
- **A user logged in**: no "Admin" in the menu; the admin pages say they're for admins.
- **Accounts only, nobody logged in**: the login page instead of any page.
- Mockup knobs (`/__mock`): log in as an admin or as a user; accounts only on or off; LDAP answering or not.

## Proposed backend (contract v2.13)

- **Accounts** gain `role` (`admin` | `user`), `source` (`local` | `ldap`), `disabled`, a display name, and a stamp
  that changes with the password, the role or `disabled` (the sessions made before are dropped at their next check).
  A directory account gets its row at its first login (no password), its role read at each login and each check.
- `GET /api/auth/me` gains `role`, `source` and `displayName`; `PUT /api/auth/me/password` (one's own, with the current
  one: a local account's, or a directory one's in the directory); `GET /api/auth/access` (open to anyone:
  `accountsOnly`, for the page to show the login page).
- `GET/POST /api/admin/accounts`, `PUT/DELETE /api/admin/accounts/{id}` (role, password, disabled).
- `GET/PUT /api/admin/settings/access` (`accountsOnly`), `GET/PUT /api/admin/settings/ldap`,
  `POST /api/admin/settings/ldap/test`.
- `[Authorize]` alone becomes an **admin's** session (today: any session); the browsing endpoints, `/art` and `/media`
  take any session when "accounts only" is on, none otherwise.
- **LDAP**: search then bind: the service account searches `(&(<uid attribute>=<name>)<filter>)` under the base DN,
  the admins filter first, then the users filter (the name escaped, RFC 4515); the user's password binds the DN found.
  `memberof` filters need a directory that answers them (LLDAP does; OpenLDAP with its memberOf overlay). The library:
  **Novell.Directory.Ldap.NETStandard** (managed code: the chiseled images have no `libldap` for
  `System.DirectoryServices.Protocols`). Timeouts of a few seconds; the login rate limit counts LDAP logins too.
- The session check every 5 minutes (cookie validation): the account there, enabled, the same stamp; a directory one
  asked again. The directory not answering keeps the session (logged); answering "not in the groups" ends it.
- The service account's password is stored as the YouTube cookie is (PostgreSQL), never sent back to the page.
- `GET /api/auth/access` also gives the password reset URL (the directory on), for the login page.

## The user's directory (probed 2026-10-25, read only)

- **LLDAP 0.5.0** on the LAN, LDAP on port 3890 (no LDAPS), its web page on 17170; base `dc=example,dc=com`. Its
  RootDSE lists the Password Modify operation (`1.3.6.1.4.1.4203.1.11.1`).
- The service account `uid=tontuyau,ou=people,dc=example,dc=com` binds, and answers `memberof` filters (a DN with
  spaces after its commas too).
- `cn=tontuyau_admin` has 2 members (one of them the service account itself); `cn=tontuyau` was made the same day,
  empty for now: the users filter matches its members once they're added.

## Delivery

1. The mockup (this branch), reviewed.
2. Then three PRs into `release/2.3.0`, an alpha after each: **roles, local accounts and My account**; **accounts
   only**; **LDAP**.

## Open (to see on the mockup)

- One "Accounts" page with three sections, or "Accounts" and "LDAP" apart in the admin menu (the mockup: apart).
- Whether a user (not an admin) sees anything new besides "My account" (watch history, preferences: later).
