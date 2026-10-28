<script lang="ts">
  // A modal dialog (2.3.0): the browser's own <dialog>, so the page behind is inert, focus stays inside, and Escape
  // closes it. `open` shows it; Escape, a click on the dimmed page or an action setting it back to false closes it.
  // Its title, what it says, and its actions on a row at the bottom, the main one first.
  import type { Snippet } from 'svelte'

  let {
    open = $bindable(false),
    title,
    children,
    actions,
  }: {
    open: boolean
    title: string
    children: Snippet
    actions: Snippet
  } = $props()

  let dialog = $state<HTMLDialogElement>()
  const uid = $props.id()

  $effect(() => {
    if (!dialog) {
      return
    }
    if (open && !dialog.open) {
      dialog.showModal()
    } else if (!open && dialog.open) {
      dialog.close()
    }
  })
</script>

<dialog
  bind:this={dialog}
  aria-labelledby="{uid}-title"
  onclose={() => (open = false)}
  onclick={(event) => {
    // The dimmed page around it: the dialog itself, not what's inside.
    if (event.target === dialog) {
      open = false
    }
  }}
>
  <div class="inside">
    <h2 id="{uid}-title">{title}</h2>
    <div class="body">{@render children()}</div>
    <div class="actions">{@render actions()}</div>
  </div>
</dialog>

<style>
  dialog {
    width: min(var(--login-width), calc(100vw - 2 * var(--space-4)));
    max-height: calc(100dvh - 2 * var(--space-8));
    padding: 0;
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-xl);
    background: var(--color-bg-elevated);
    color: var(--color-text);
    box-shadow: var(--shadow-menu);
  }

  dialog::backdrop {
    background: var(--color-scrim);
  }

  .inside {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-5) var(--space-6);
  }

  h2 {
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-medium);
  }

  .body {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    color: var(--color-text-secondary);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: var(--space-2);
  }
</style>
