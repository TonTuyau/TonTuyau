# TonTuyau 2.4.0 — plan (draft)

The frontend reworked on an existing design system, ESLint and Prettier replaced by Biome, and the command line
removed. **A trial first** (`work/2.4.0-trial`, local only, as the mockups): Skeleton tried on one card and one popup
under the real Content-Security-Policy, before the choice is final.

## Today

- **The frontend**: Svelte 5 and Vite, 66 components (about 15,000 lines), all of their look our own: the tokens in
  `src/theme/youtube.css` (about 175 `var(--…)`, light and dark through `light-dark()`), `base.css`, and each
  component's scoped CSS. The popups are built by hand: the help bubbles (`HelpButton`), the settings and account
  menus, the drawer, the dialogs.
- **The Content-Security-Policy** (`server/server.ts`): no inline script, no `<style>`, no `style=""`, no `data:` URL,
  stylesheets included; `check.sh` checks the build for them. Dynamic styles go through the CSSOM (`element.style`),
  which a CSP doesn't restrict.
- **Lint and format**: ESLint 10 (`@eslint/js` recommended, typescript-eslint strict, eslint-plugin-svelte
  recommended), Prettier with `prettier-plugin-svelte` (no semicolons, single quotes, 120 columns); svelte-check types
  the code.
- **The command line**: `tontuyau` in the backend's image (`status`, `youtube …`, `import`, `index rebuild`, `health`,
  `--version`), talking to the backend through a Unix socket (`backend/src/TonTuyau.Api/Cli/`: `CliController` under
  `/cli/…`, `CliOnlyFilter`, `SocketCliGate`, `BackendClient`…). `tontuyau health` is the backend container's health
  check (the image has no shell nor curl since 1.5.1). The Tasks page shows each task's command
  (`frontend/src/lib/runs.ts`: `tontuyau youtube download <id|link>`…).

## Decided (2026-10-30)

1. **An existing design system** rather than our own components: **Skeleton** (5.x, on Tailwind 4), kept after the
   trial (below).
2. **ESLint and Prettier removed; Biome lints and formats everything**, `.svelte` files included (its full support is
   still marked experimental: `html.experimentalFullSupportEnabled` and `html.formatter.enabled`).
3. **The command line removed**, except **`tontuyau health`**, kept as a normal command (not hidden): the container's
   health check.
4. **Floating UI for every popup's position**, none measured by hand: Skeleton's Popover, Menu, Tooltip, Combobox
   already place theirs with it (Zag.js's positioning is `@floating-ui/dom`); the popups that stay ours use
   `@floating-ui/dom` directly.
5. **Lucide for the icons** (`@lucide/svelte`), in place of `Icon.svelte` and its Material Symbols.
6. **The admin pages take Skeleton's look**, as in the trial; the public pages keep YouTube's.
7. **LaPompe moves to Biome too**, in a release of its own.
8. **As much Skeleton as possible**: wherever Skeleton has a part for something, it's used, inside our own components
   too (the player's included); our own code only where Skeleton has nothing.
9. **No reinventing the wheel** (a rule for all of TonTuyau from now on, backend included): **if a maintained
   library does it, it's used**. Our own code only where none does, or for a few lines of a browser API (Intl,
   IntersectionObserver, the clipboard). "Maintained", as proposed: a release in the last year, its issues answered,
   not one person's abandoned repo, a licence that goes with GPL-3.0-or-later, and it works under our
   Content-Security-Policy (checked in the browser, as Skeleton was).

## The Skeleton trial (2026-10-30)

Skeleton 5.0.1 and Tailwind 4.3.3 added beside today's CSS; a `tontuyau` Skeleton theme generated from `youtube.css`
(the teal as primary, YouTube's greys as surface, white and `#0f0f0f` at the ends); the YouTube page's **Downloading**
card (form classes, checkboxes, a Switch, a toast once saved) and **`HelpButton`** (Skeleton's Popover) rebuilt. Built
in mockup mode and served by `vite preview` with the production server's policy, exactly.

- **The policy holds.** The build passes `check-csp-html` (Skeleton's only `data:` URLs are its `mask-*` utilities,
  written out only when used), and the browser reported no violation while the popup, the switch and the toasts
  worked: Zag.js (under Skeleton's components) positions and animates through Svelte's style path, which sets
  `element.style.cssText` (the CSSOM), as our own dynamic styles already do.
- **The Popover does what `HelpButton` did by hand**: placed under the "?" and kept on the screen, Esc closes it with
  the focus back on the "?", a click outside closes it, `aria-expanded` / `aria-controls` set. 171 lines became about
  35, with no CSS of its own.
- **Light and dark**: Skeleton resolves its colours with `light-dark()`, as our tokens do; the three states (the
  device's, forced light, forced dark) work. But Skeleton's theme name takes `data-theme` on `<html>`: a forced light or
  dark moves to **`data-mode`** (`public/theme-init.js`, `src/lib/theme.svelte.ts`, `youtube.css`).
- **The Switch** is a visually hidden checkbox with its label: the tests find it by role `checkbox` and its name, as
  before. **The toasts** work (a success one lasts 2 s by default: to lengthen).
- **The stylesheet grows**: 126 KB → 282 KB (17 KB → 32 KB gzipped) with Tailwind and all of Skeleton's styles; less
  once our own CSS is gone, and Skeleton's parts can be imported one by one.
- **Tests**: 9 of 812 failed, none a bug: 7 for `data-theme` → `data-mode`; 2 from the Popover (its bubble is moved
  to `<body>`, so a card's text no longer includes its help; the focus comes back on the next frame: a `waitFor`).
- **Names Tailwind uses too**: `--font-mono`, `--font-weight-medium` / `-bold`, `--radius-sm` / `-md` / `-lg` / `-xl`.
  Ours are outside any layer, so they win for now (`rounded-md` takes our radius); in the rework they become the
  theme's.
- **Our class names that are Tailwind's or Skeleton's too get their styles**: Tailwind writes out a utility for any
  of its names it finds in the code, and the rule applies to every element with that class, ours included. Seen in the
  trial: the logo's `mark` (Skeleton's highlighted text: a grey square behind the logo) and the rail's `label`
  (`display: block; width: 100%`: the labels out of line under their icons). 13 names in 31 components: `badge`,
  `block`, `card`, `chip`, `disabled`, `filter`, `grid`, `inline`, `label`, `legend`, `placeholder`, `sub`, `time`
  (and `mark`). The trial prefixes them `tt-` in the components not rewritten yet; the base PR does the same, and a
  check (the build's unscoped rules against our class names) keeps new ones out until the old CSS is gone.
- **Both styles side by side**: our `base.css` goes in a layer under Tailwind's, so a component rewritten with
  utilities isn't overridden by it; a component not rewritten yet keeps its own scoped CSS. The migration can go one
  component at a time, the app whole after each step.

## Proposed: the design system

- **The theme**: `src/theme/skeleton-tontuyau.css` (generated in the trial; tuned with Skeleton's theme generator).
  `youtube.css`'s tokens become the theme's values or Tailwind `@theme` values, and go once nothing reads them;
  `tokens.test.ts` and `branding.test.ts` follow.
- **What Skeleton takes over**: everything it has a part for (decision 8; the list per component below), the player's
  controls included.
- **What stays ours**: only what Skeleton has nothing for: the video itself, the grids, infinite scroll, links in
  text, the import chart, the logo.
- **The YouTube look stays on the public pages**, through the theme: radius, the pill buttons, the greys,
  Roboto/Arial. The admin pages take Skeleton's look (decision 6).
- **Icons**: Lucide's Svelte components where `<Icon name=…>` is today; `Icon.svelte` and `src/assets/icons` go.
- **Popups**: positioned by Floating UI (decision 4). With the player's menus on Skeleton's Menu, every popup is a
  Skeleton part; `@floating-ui/dom` directly only for one Skeleton can't do, if any turns up.
- **The policy doesn't change**; `check.sh` checks every build, a browser run under the policy before each PR.

## The components folder after the rework

47 components today (decision 8: a Skeleton part wherever one exists).

- **6 go**, Skeleton's or Lucide's parts used where they're used: `Button` (`btn`), `Badge` (`badge`), `Icon` (Lucide),
  `Skeleton` (`placeholder`), `Chips` (Segmented Control, its items rendered as links: the choice stays in the URL),
  `HoldBar` (Progress, in the task's card).
- **35 keep their file, made of Skeleton's parts** (what's ours: the data, the labels, the app's rules):
  - `HelpButton` (Popover), `CopyButton` (button, Tooltip "Copied"), `RunStatePill` and `YouTubeStateBadge` (badge
    presets: a state → a colour), `Avatar` (Avatar, our `srcset`).
  - `TopBar` (App Bar), `Sidebar` and `BottomNav` (Navigation: rail, bar, sidebar; its items links),
    `AdminShell` (App Bar, Navigation; its drawer a Dialog), `AccountMenu` and `SettingsMenu` (Menu; the theme a
    Segmented Control, the language radio items), `SearchBox` and `ChannelFilter` (Combobox).
  - `AddChannelCard`, `DownloadSettings`, `SubscriptionCard`, `TaskCard`, `YouTubeTaskCard` (cards, form fields,
    switches, Dialog to confirm, toasts), `FailedDownloads` and `ChannelTable` (`table`, Pagination where it pages),
    `StorageCard` (Progress / meter), `RunQueue` (cards, badges, Menu for its actions), `RunLogView` (`table`
    lines, badges for the levels).
  - `ErrorState`, `StateMessage`, `MissingState` (card, `btn`).
  - `ExpandableText` (Collapsible: Zag's `collapsedHeight` shows its first lines), `Chapters` and `Comments`
    (Collapsible for the long lists and the answers, Avatar), `VideoCard`, `ShortCard`, `ChannelRow` (card, Avatar,
    badges, `placeholder` while loading), `Thumbnail` (`placeholder`).
  - `PlayerControls`, `ShortsControls`: **kept as they are in 2.4.0** (the user, 2026-10-30), only restyled with the
    theme and given Lucide's icons; Video.js 10 seen again later.
- **6 with no Skeleton part**, Skeleton has nothing for their job (other libraries may: below): `Player` (the `<video>`, hls.js, choosing how to
  play), `VideoList` (the grids), `LoadMore` (infinite scroll), `LinkifiedText` (links and timecodes in text),
  `ImportChart` (the chart), `Logo`.

Across the pages too: the 22 `title=""` hints become Tooltips, the 23 inline confirmations (delete a key, an account,
cancel a run…) Dialogs, the channel page's and admin pages' tabs Tabs (their triggers links: a tab stays a URL).

## Our own code a library may do (decision 9: each checked, the choice yours)

What 2.4.0's frontend still writes itself, and the libraries to look at. Each is checked against decision 9 (still
maintained, licence, the CSP) before it replaces ours; one that fails keeps our code.

| Ours today | Lines | Libraries to check | Chosen (2026-10-30) |
|---|---|---|---|
| `PlayerControls`, `ShortsControls` (the player's controls) | 1,400 | Media Chrome, Vidstack, Video.js | **Ours kept in 2.4.0** (2026-10-30); Video.js 10 (`@videojs/html` 10.2.0, 2026-10-22, Apache-2.0) looked at, for later: its trial below |
| `lib/i18n` (typed dictionaries, plurals) | 75 + the dictionaries | svelte-i18n, Paraglide JS | **Paraglide JS** (2.29.3, 2026-10-28, MIT): svelte-i18n was chosen first, then left for Paraglide, svelte-i18n having had no release since 2024-10-21 (decision 9) |
| `lib/remote.svelte.ts` (an API call's loading state, cursor pages) | 174 | TanStack Query for Svelte | **TanStack Query** (`@tanstack/svelte-query` 6.3.6, 2026-10-26, MIT, Svelte ≥ 5.25) |
| `lib/api/client.ts` (calls typed from the OpenAPI file) | — | `openapi-fetch` | **Ours kept**, typed by `openapi-typescript` as today |
| `lib/router` (History API router) | 98 + 214 of routes | a maintained Svelte 5 SPA router, if one passes decision 9 | open |
| `lib/linkify.ts` (URLs in descriptions as links) | 118 | linkifyjs | **linkifyjs** (4.4.0, 2026-10-23, MIT; the timecodes stay ours) |
| `ImportChart` (the import history's bars) | 332 | LayerChart, Chart.js | **LayerChart** (2.9.1, 2026-10-23, MIT, Svelte 5) |
| `LoadMore` (infinite scroll) | 46 | — | **Ours kept** (IntersectionObserver; no Pagination) |

### Video.js 10 under the policy (trial, 2026-10-30; for later: our player stays in 2.4.0)

Its default video skin on a mock video (`vjs-trial.html` in the trial), served with the production policy: it
**plays, seeks, and its settings menu opens and places itself** (CSS anchor positioning, set through the CSSOM), and
the skins' own styles pass (constructable stylesheets, `adoptedStyleSheets`, which a CSP doesn't restrict). But the
policy blocks **6 `<style>` elements and 18 `style=""` attributes** at load (its docs say "some player UI" needs
`style-src 'unsafe-inline'`):
- a `<style id="__media-styles">` added to `<head>` (644 characters: `@property` for the sliders' fill and buffer,
  `display: contents` on its wrappers, the video's size);
- a `<style>` inside the poster's and the thumbnail preview's shadow roots;
- its icons, SVG strings with a `<style>` and `style=""` (the spinner's and AirPlay's animations).

Ways through, to choose:
1. **Keep our policy, give Video.js its styles ourselves**: `__media-styles` as one of our stylesheets, under that id
   (Video.js adds its own only when no element has it); a TonTuyau skin (Video.js 10's skins are meant to be swapped)
   with Lucide icons in place of its animated ones; the poster and thumbnail styled from outside, or not used (our
   `Thumbnail` makes the poster). To check: what the blocked rules change on screen.
2. **Hashes**: each of those static `<style>` contents allowed by its `sha256` (`style-src-elem`), the attributes by
   `'unsafe-hashes'` (`style-src-attr`); exact, but the hashes change with each Video.js release (computed by a check
   from the installed package).
3. **`style-src 'unsafe-inline'`**, as Video.js documents: simplest, and the end of QA14-3's strict policy for styles
   (scripts stay strict).

## Proposed: Biome

- `biome.json`: the linter (recommended rules), the formatter as Prettier is set today (no semicolons, single quotes,
  120 columns), the full support for `.svelte`. Out: `eslint`, `@eslint/js`, `eslint-plugin-svelte`,
  `typescript-eslint`, `globals`, `prettier`, `prettier-plugin-svelte`, `eslint.config.js`, `.prettierrc.json`.
- Scripts: `lint` = `biome ci`, `format` = `biome format --write`; `api:types` and `mock:fixtures` format with Biome;
  `check.sh` and the CI's Frontend check follow.
- **The reformatting in a commit of its own** (it touches every file), apart from the configuration, so the rest of
  the PR stays readable.
- What goes with ESLint: eslint-plugin-svelte's rules (svelte-check still gives the compiler's warnings; the code has
  no `{@html}`) and typescript-eslint's strict rules Biome doesn't have; the one `eslint-disable` goes.

## Proposed: the command line

- **Backend**: `Cli/` goes but for `HealthCheck`: `tontuyau health` calls `/health` and exits 0 or 1; any other
  argument: the usage, exit 2. The Unix socket, `SocketCliGate`, `CliOnlyFilter` and the `/cli/…` endpoints go.
- **Runs**: the trigger `cli` stays in the contract, for the runs already in the history; no new run gets it.
- **Tasks page**: the command under each task (`runs.ts`) goes.
- **Integration tests**: 9 feature files run `tontuyau …` (`Cli`, `Download`, `DiskSpace`, `RefreshAll`,
  `IndexAutoRebuild`, `YouTubeState`, `Subscriptions`, `YouTubeSync`, `YouTubeRefresh`): their steps start the same
  tasks through the API; `Cli.feature` keeps `health` only.
- **README**: "Command line" says `tontuyau health` only; importing TubeArchivist's library points to the Tasks page
  (no more `docker exec … tontuyau import`).

## Delivery

1. This plan, reviewed; the trial seen (`work/2.4.0-trial`, never pushed).
2. Then PRs into `release/2.4.0` (from `main` once 2.3.0 is released): **Biome** (`work/2.4.0-biome`, first, so the
   new code is formatted once); **the command line** (`work/2.4.0-cli`); **the design system's base** (Tailwind,
   Skeleton, the theme, `data-mode`, the layers, the shared parts); then **the pages, by area** (admin, then the public
   ones), each seen in the mockup before its PR. Alphas when you say.

## Open

- **The areas' order** and how many PRs for the public pages.
- **Later, not 2.4.0**: Video.js 10 for the player, and then one of the three ways above for the policy.
- **The router**.
