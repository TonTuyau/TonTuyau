<script lang="ts">
  // "This isn't here": the not-found page, and an unknown or malformed id on the watch, shorts and channel pages
  // (the API's 404 / 400). No retry: the answer would be the same. The page sets its own tab title.
  import { t } from '../lib/i18n/i18n.svelte'
  import { href } from '../lib/router/routes'
  import Button from './Button.svelte'
  import StateMessage from './StateMessage.svelte'

  let { title, detail }: { title: string; detail?: string } = $props()
</script>

<StateMessage icon="broken" {title} {detail} role="status">
  <Button href={href.home()}>{t('notFound.home')}</Button>
</StateMessage>
