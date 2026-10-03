import { getRelativeLocaleUrl } from 'astro:i18n';
import { messages as english, type Messages as CatalogueMessages } from './en';
import { messages as polish } from './pl';

export type Locale = 'pl' | 'en';
export type Messages = CatalogueMessages;

export function getMessages(locale: Locale): Messages {
  return locale === 'pl' ? polish : english;
}

export function localePath(locale: Locale, path = ''): string {
  return getRelativeLocaleUrl(locale, path.replace(/^\/+|\/+$/g, ''));
}
