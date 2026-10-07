<script lang="ts">
  import { t } from '../lib/i18n/i18n.svelte'
  import { href } from '../lib/router/routes'
</script>

<a class="logo" href={href.home()} aria-label={t('app.home')}>
  <!-- TonTuyau's mark: a plumbing pipe ("tuyau") with an elbow and two flanges, a play triangle flowing out of its
       mouth. Flat, one colour (the brand teal); unlike YouTube's logo (no red rounded rectangle). public/favicon.svg
       draws the same shapes. -->
  <svg class="mark" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
    <path class="pipe" d="M5.5 21.5V12a5 5 0 0 1 5-5h2" />
    <rect class="flange" x=".5" y="19.5" width="10" height="3" rx=".75" />
    <rect class="flange" x="12" y="2" width="2.75" height="10" rx=".75" />
    <path class="play" d="M17.5 2.75l5.75 4.25-5.75 4.25z" />
  </svg>
  <span class="name">{t('app.name')}</span>
</a>

<style>
  .logo {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    padding: var(--space-2);
    border-radius: var(--radius-md);
  }

  .mark {
    width: calc(var(--icon-size) * 1.15);
    height: calc(var(--icon-size) * 1.15);
  }

  .pipe {
    fill: none;
    stroke: var(--color-brand);
    stroke-width: 8;
  }

  .flange {
    fill: var(--color-brand);
  }

  /* Rounded corners, like the flanges'. */
  .play {
    fill: var(--color-brand);
    stroke: var(--color-brand);
    stroke-width: 1.5;
    stroke-linejoin: round;
  }

  .name {
    font-size: var(--font-size-xl);
    font-weight: var(--font-weight-bold);
    letter-spacing: -0.04em;
  }
</style>
