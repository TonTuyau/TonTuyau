import type { en } from './en'

export type Lang = 'en' | 'fr'

export const languages: readonly Lang[] = ['en', 'fr']

/** A plural entry: the form is chosen with Intl.PluralRules for the current language. */
export interface Plural {
  one: string
  other: string
}

type Reference = typeof en

/** The shape every dictionary must have: the English keys, with string or plural values. */
export type Dictionary = { [K in keyof Reference]: Reference[K] extends string ? string : Plural }

export type TextKey = { [K in keyof Reference]: Reference[K] extends string ? K : never }[keyof Reference]
export type PluralKey = Exclude<keyof Reference, TextKey>

export type Params = Record<string, string | number>
