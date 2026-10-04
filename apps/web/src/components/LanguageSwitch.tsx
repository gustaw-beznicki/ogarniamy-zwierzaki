import { useEffect, useState } from 'react';
import type { Locale, Messages } from '../i18n';

const locales: { id: Locale; name: string }[] = [
  { id: 'pl', name: 'Polski' },
  { id: 'en', name: 'English' },
];

// Polish pages live at the root and English ones under /en/, so switching only swaps that prefix.
function pathFor(target: Locale, pathname: string): string {
  const base = pathname.replace(/^\/en(?=\/|$)/, '') || '/';
  return target === 'en' ? `/en${base}` : base;
}

export function LanguageSwitch({ locale, messages, pathname }: { locale: Locale; messages: Messages; pathname: string }) {
  // Static pages resolve their resource from the query string, so switching keeps it once the page is hydrated.
  const [search, setSearch] = useState('');
  useEffect(() => { setSearch(window.location.search); }, []);
  return <nav className="language-switch" aria-label={messages.languageSwitchLabel}>
    {locales.map((item) => <a
      key={item.id}
      href={`${pathFor(item.id, pathname)}${search}`}
      className={item.id === locale ? 'active' : undefined}
      aria-current={item.id === locale ? 'true' : undefined}
      aria-label={item.name}
      lang={item.id}
      hrefLang={item.id}
    >{item.id.toUpperCase()}</a>)}
  </nav>;
}
