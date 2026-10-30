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

1. **An existing design system** rather than our own components: **Skeleton** (5.x, on Tailwind 4), the user's
   choice, to be confirmed by the trial (below).
2. **ESLint and Prettier removed; Biome lints and formats everything**, `.svelte` files included (its full support is
   still marked experimental: `html.experimentalFullSupportEnabled` and `html.formatter.enabled`).
3. **The command line removed**, except **`tontuyau health`**, kept as a normal command (not hidden): the container's
   health check.
4. **Floating UI for every popup's position**, none measured by hand: Skeleton's Popover, Menu, Tooltip, Combobox
   already place theirs with it (Zag.js's positioning is `@floating-ui/dom`); the popups that stay ours use
   `@floating-ui/dom` directly.

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
- **What Skeleton takes over**: buttons, form fields, checkboxes and switches, the help bubbles (Popover), the menus
  (settings, account: Menu), the dialogs and the drawer (Dialog), toasts, badges and state pills, tabs, pagination
  ("Load more" stays ours), tables, progress bars.
- **What stays ours** (no such component, or YouTube's own look): the player and its controls, the shorts, the video
  grid and cards, thumbnails, chapters, the import chart. Restyled with the theme's utilities, not Skeleton parts.
- **The YouTube look stays**, through the theme: radius, the pill buttons, the greys, Roboto/Arial. Skeleton's
  defaults where YouTube has nothing to copy (the admin pages).
- **Popups**: positioned by Floating UI (decision 4): Skeleton's own parts, or `@floating-ui/dom` for ours (the player's
  menus in `PlayerControls` and `ShortsControls`, the only ones still measuring the screen by hand).
- **The policy doesn't change**; `check.sh` checks every build, a browser run under the policy before each PR.

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

- **Skeleton confirmed** after seeing the trial (or shadcn-svelte: the same Tailwind base, its code copied into the
  repo).
- **Icons**: our `Icon.svelte` (our own SVG paths) or Lucide (`@lucide/svelte`, what Skeleton's examples use).
- **How close to YouTube** the admin pages stay, or Skeleton's look there.
- **LaPompe** uses ESLint and Prettier too: Biome there as well, in its own release?
- **The areas' order** and how many PRs for the public pages.
