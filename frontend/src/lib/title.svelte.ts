import { t } from './i18n/i18n.svelte'

/** Sets the tab title to "<page> - TonTuyau" (or "TonTuyau" alone) while the calling component lives. */
export function pageTitle(get: () => string | null): void {
  $effect(() => {
    const page = get()
    document.title = page ? `${page} - ${t('app.name')}` : t('app.name')
  })
}
