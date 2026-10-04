import { useState, type ReactNode } from 'react';
import type { Locale, Messages } from '../i18n';
import { clientLocalePath } from '../i18n/paths';
import { api } from '../lib/api';

type Active = 'search' | 'add' | 'animals';
export function AppShell({ locale, messages, active, children }: {
  locale: Locale;
  messages: Messages;
  active: Active;
  children: ReactNode;
}) {
  const [signOutFailed, setSignOutFailed] = useState(false);
  const nav: { id: Active; href: string; label: string; icon: string }[] = [
    { id: 'search', href: clientLocalePath(locale, 'search'), label: messages.search, icon: '⌕' },
    { id: 'add', href: clientLocalePath(locale, 'add'), label: messages.add, icon: '+' },
    { id: 'animals', href: clientLocalePath(locale, 'animals'), label: messages.animals, icon: '♧' },
  ];
  async function signOut() {
    setSignOutFailed(false);
    try {
      await api.logout();
      window.location.assign(clientLocalePath(locale, 'signin'));
    } catch {
      setSignOutFailed(true);
    }
  }
  return <div className="app-shell">
    <aside className="sidebar">
      <a className="brand" href={clientLocalePath(locale)}>{messages.appName}</a>
      <nav aria-label={messages.navigationLabel}>{nav.map((item) => <a key={item.id} className={active === item.id ? 'nav-link active' : 'nav-link'} href={item.href}><span aria-hidden="true">{item.icon}</span>{item.label}</a>)}</nav>
      <div className="sidebar-bottom"><button className="sign-out" onClick={() => void signOut()}>{messages.signOut}</button></div>
    </aside>
    <div className="main-area">{signOutFailed && <p className="error shell-error" role="alert"><span aria-hidden="true">ⓘ</span> {messages.signOutFailed}</p>}{children}</div>
    <nav className="tab-bar" aria-label={messages.navigationLabel}>{nav.map((item) => <a key={item.id} className={active === item.id ? 'tab-link active' : 'tab-link'} href={item.href}><span aria-hidden="true">{item.icon}</span>{item.label}</a>)}<button className="tab-link" onClick={() => void signOut()} aria-label={messages.signOut}>↪</button></nav>
  </div>;
}
