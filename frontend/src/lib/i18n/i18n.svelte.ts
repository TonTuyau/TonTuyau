// The current language and the translate functions. Reading `i18n.lang` inside t()/tp() makes every
// template that translates re-render when the language changes.
import { readPreference, writePreference } from '../storage'
import { en } from './en'
import { fr } from './fr'
import { languages, type Dictionary, type Lang, type Params, type PluralKey, type TextKey } from './types'

export const dictionaries: Record<Lang, Dictionary> = { en, fr }

export const LANG_STORAGE_KEY = 'tontuyau.lang'

const fallback: Lang = 'en'

function isLang(value: string | null): value is Lang {
  return value !== null && (languages as readonly string[]).includes(value)
}

/** The first browser language we have a dictionary for (`fr-CA` → `fr`), else English. */
export function detectLanguage(browserLanguages: readonly string[]): Lang {
  for (const tag of browserLanguages) {
    const base = tag.toLowerCase().split('-')[0] ?? ''
    if (isLang(base)) {
      return base
    }
  }
  return fallback
}

export function initialLanguage(): Lang {
  const saved = readPreference(LANG_STORAGE_KEY)
  if (isLang(saved)) {
    return saved
  }
  const browser = globalThis.navigator?.languages ?? [globalThis.navigator?.language ?? fallback]
  return detectLanguage(browser)
}

export function interpolate(template: string, params: Params = {}): string {
  return template.replace(/\{(\w+)\}/g, (match, name: string) => (name in params ? String(params[name]) : match))
}

class I18n {
  lang = $state<Lang>(initialLanguage())

  setLang(lang: Lang): void {
    this.lang = lang
    writePreference(LANG_STORAGE_KEY, lang)
    applyDocumentLang(lang)
  }
}

function applyDocumentLang(lang: Lang): void {
  if (typeof document !== 'undefined') {
    document.documentElement.lang = lang
  }
}

export const i18n = new I18n()
applyDocumentLang(i18n.lang)

/** Translates a text key. `params` fill the `{name}` placeholders. */
export function t(key: TextKey, params?: Params): string {
  return interpolate(dictionaries[i18n.lang][key], params)
}

/**
 * Translates a plural key: the form is chosen from `count`; `{count}` shows `display` when given
 * (e.g. the compact "74K"), else the number formatted for the language.
 */
export function tp(key: PluralKey, count: number, display?: string, params?: Params): string {
  const lang = i18n.lang
  const entry = dictionaries[lang][key]
  const form = new Intl.PluralRules(lang).select(count) === 'one' ? entry.one : entry.other
  return interpolate(form, { ...params, count: display ?? new Intl.NumberFormat(lang).format(count) })
}
