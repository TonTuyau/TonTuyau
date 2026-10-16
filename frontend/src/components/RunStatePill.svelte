<script lang="ts">
  // A run's state (1.4): running, succeeded, failed, canceled, on hold (1.5), as a coloured pill with its word.
  import type { RunState } from '../lib/api/types'
  import { t } from '../lib/i18n/i18n.svelte'

  let { state }: { state: RunState } = $props()
</script>

<span class="pill {state}">{t(`run.state.${state}`)}</span>

<style>
  .pill {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    height: var(--badge-height);
    padding: 0 var(--space-2);
    border-radius: var(--radius-pill);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
    white-space: nowrap;
  }

  .pill::before {
    content: '';
    width: var(--space-2);
    height: var(--space-2);
    border-radius: var(--radius-round);
    background: currentColor;
  }

  .succeeded {
    background: var(--color-success-bg);
    color: var(--color-success);
  }

  .failed {
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .canceled {
    background: var(--color-surface);
    color: var(--color-text-secondary);
  }

  .onHold {
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
  }

  .running {
    background: var(--color-running-bg);
    color: var(--color-running);
  }

  @media (prefers-reduced-motion: no-preference) {
    .running::before {
      animation: beat 1.2s ease-in-out infinite;
    }
  }

  @keyframes beat {
    50% {
      opacity: 0.25;
    }
  }
</style>
