<script lang="ts" module>
  // Material Symbols, Rounded, weight 400: Google's own SVG files, as they come, in src/assets/icons (see its README).
  // Not the classic Material Icons YouTube's look comes from.
  const files = import.meta.glob('../assets/icons/*.svg', { query: '?raw', import: 'default', eager: true }) as Record<
    string,
    string
  >

  /** Each icon's file in src/assets/icons, by Google's name; `<name>-fill.svg` next to it is its filled form. */
  const symbols = {
    menu: 'menu',
    dashboard: 'dashboard',
    key: 'key',
    smartDisplay: 'smart_display',
    tasks: 'schedule',
    search: 'search',
    home: 'home',
    // Our own vertical-video glyph (a portrait frame with a play triangle), not YouTube's Shorts logo.
    shorts: 'tontuyau_shorts',
    channels: 'subscriptions',
    videos: 'video_library',
    streams: 'sensors',
    download: 'download',
    close: 'close',
    back: 'arrow_back',
    tune: 'tune',
    translate: 'translate',
    like: 'thumb_up',
    warning: 'warning',
    person: 'person',
    movie: 'movie',
    broken: 'broken_image',
    check: 'check',
    // The player's own controls.
    play: 'play_arrow',
    pause: 'pause',
    volumeUp: 'volume_up',
    volumeDown: 'volume_down',
    volumeOff: 'volume_off',
    fullscreen: 'fullscreen',
    fullscreenExit: 'fullscreen_exit',
    settings: 'settings',
    loop: 'repeat',
    speed: 'speed',
    chapters: 'list',
    visibility: 'visibility',
    visibilityOff: 'visibility_off',
    logout: 'logout',
    more: 'more_vert',
    appearance: 'dark_mode',
    chevron: 'chevron_right',
    up: 'arrow_upward',
    down: 'arrow_downward',
    playerLarge: 'fit_page_width',
    playerSmall: 'width_normal',
    // 2.3.0: the accounts; the admin pages refused to a user.
    accounts: 'group',
    lock: 'lock',
  } as const

  export type IconName = keyof typeof symbols

  /** The one path of an icon file, or null when there's no such file. */
  export function iconPath(file: string): string | null {
    const svg = files[`../assets/icons/${file}.svg`]
    return svg === undefined ? null : (/\sd="([^"]+)"/.exec(svg)?.[1] ?? null)
  }

  export const iconFiles: Readonly<Record<IconName, string>> = symbols
</script>

<script lang="ts">
  let { name, filled = false }: { name: IconName; filled?: boolean } = $props()

  // The filled form only where there's one (the navigation's icons): the others stay outlined.
  const d = $derived((filled ? iconPath(`${symbols[name]}-fill`) : null) ?? iconPath(symbols[name]) ?? '')
</script>

<svg class="icon" viewBox="0 -960 960 960" aria-hidden="true" focusable="false"><path {d} /></svg>

<style>
  .icon {
    width: var(--icon-size);
    height: var(--icon-size);
    fill: currentColor;
    flex: none;
  }
</style>
