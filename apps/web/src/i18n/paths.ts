import type { Locale } from './index';

export function clientLocalePath(locale: Locale, path = ''): string {
  const normalized = path.replace(/^\/+|\/+$/g, '');
  const prefix = locale === 'en' ? '/en' : '';
  return normalized ? `${prefix}/${normalized}/` : `${prefix || ''}/`;
}
