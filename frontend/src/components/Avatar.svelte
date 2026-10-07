<script lang="ts">
  // A round channel picture from the API's URL, with its resized versions as `srcset` sized for the avatar's size
  // (v1.4); a generic person icon when there's none or it fails.
  import type { ImageSize } from '../lib/api/types'
  import { imageSizes, srcset } from '../lib/srcset'
  import Icon from './Icon.svelte'

  let {
    src,
    versions = [],
    size = 'sm',
  }: { src: string | null; versions?: readonly ImageSize[]; size?: 'xs' | 'sm' | 'md' | 'lg' } = $props()

  let failed = $state(false)
  $effect(() => {
    void src
    failed = false
  })
</script>

<span class="avatar {size}">
  {#if src && !failed}
    <img
      {src}
      srcset={srcset(versions)}
      sizes={srcset(versions) ? imageSizes.avatar[size] : undefined}
      alt=""
      loading="lazy"
      decoding="async"
      onerror={() => (failed = true)}
    />
  {:else}
    <span class="placeholder" data-testid="avatar-placeholder"><Icon name="person" /></span>
  {/if}
</span>

<style>
  .avatar {
    display: inline-block;
    flex: none;
    overflow: hidden;
    border-radius: var(--radius-round);
    background: var(--color-skeleton);
  }

  .xs {
    width: var(--avatar-xs);
    height: var(--avatar-xs);
  }

  .sm {
    width: var(--avatar-sm);
    height: var(--avatar-sm);
  }

  .md {
    width: var(--avatar-md);
    height: var(--avatar-md);
  }

  .lg {
    width: var(--avatar-lg);
    height: var(--avatar-lg);
  }

  img {
    width: 100%;
    height: 100%;
    object-fit: cover;
  }

  .placeholder {
    display: grid;
    place-items: center;
    width: 100%;
    height: 100%;
    color: var(--color-text-secondary);
  }
</style>
