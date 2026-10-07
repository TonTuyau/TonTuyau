<script lang="ts">
  import MissingState from '../components/MissingState.svelte'
  import { t } from '../lib/i18n/i18n.svelte'
  import { pageTitle } from '../lib/title.svelte'

  pageTitle(() => t('notFound.title'))
</script>

<MissingState title={t('notFound.title')} />
