// The app's main destinations, shared by the left guide (full / mini rail / drawer) and the phone bottom bar.
import type { IconName } from '../components/Icon.svelte'
import type { TextKey } from './i18n/types'
import { href, type Route } from './router/routes'

export interface NavItem {
  label: TextKey
  icon: IconName
  url: string
  current: boolean
}

/** `main` then `explore`, as in the guide's two sections; `current` marks the item for this route. */
export function navItems(route: Route): { main: NavItem[]; explore: NavItem[] } {
  const feed = (type: 'video' | 'short' | 'stream') => route.name === 'feed' && route.type === type
  return {
    main: [
      { label: 'nav.home', icon: 'home', url: href.home(), current: route.name === 'home' },
      {
        label: 'nav.shorts',
        icon: 'shorts',
        url: href.feed('short'),
        current: feed('short') || route.name === 'shorts',
      },
      {
        label: 'nav.channels',
        icon: 'channels',
        url: href.channels(),
        current: route.name === 'channels' || route.name === 'channel',
      },
    ],
    explore: [
      { label: 'nav.videos', icon: 'videos', url: href.feed('video'), current: feed('video') },
      { label: 'nav.streams', icon: 'streams', url: href.feed('stream'), current: feed('stream') },
    ],
  }
}
