import { useState, type ReactNode } from 'react';
import type { Locale, Messages } from '../i18n';
import { clientLocalePath } from '../i18n/paths';
import { api } from '../lib/api';
import { Alert } from './Alert';
import { BrandMark, Icon, type IconName } from './Icon';

type Active = 'search' | 'add' | 'animals';
export function AppShell({ locale, messages, active, children }: {
  locale: Locale;
  messages: Messages;
  active: Active;
  children: ReactNode;
}) {
  const [signOutFailed, setSignOutFailed] = useState(false);
  const nav: { id: Active; href: string; label: string; icon: IconName }[] = [
    { id: 'search', href: clientLocalePath(locale, 'search'), label: messages.search, icon: 'search' },
    { id: 'add', href: clientLocalePath(locale, 'add'), label: messages.add, icon: 'plus' },
    { id: 'animals', href: clientLocalePath(locale, 'animals'), label: messages.animals, icon: 'paw' },
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
  // Only one of the two placements is visible at a time (sidebar from 768 px, main column below), so the hidden copy
  // is out of the accessibility tree and the failure is announced once, next to the sign-out control in view.
  const signOutError = (compact: boolean, className: string) => signOutFailed
    ? <Alert variant="error" compact={compact} className={className}>{messages.signOutFailed}</Alert>
    : null;
  return <div className="app-shell">
    <aside className="sidebar">
      <a className="brand" href={clientLocalePath(locale)}>
        <span className="brand-tile brand-tile-accent"><BrandMark size={26} /></span>
        <span className="brand-name display">{messages.appName}</span>
      </a>
      <nav aria-label={messages.navigationLabel}>{nav.map((item) => <a
        key={item.id}
        className={active === item.id ? 'nav-link active' : 'nav-link'}
        href={item.href}
        aria-current={active === item.id ? 'page' : undefined}
      ><Icon name={item.icon} size={22} />{item.label}</a>)}</nav>
      <div className="sidebar-bottom">
        {signOutError(true, 'sidebar-error')}
        <button className="sign-out" type="button" onClick={() => void signOut()}><Icon name="logout" size={22} />{messages.signOut}</button>
      </div>
    </aside>
    <div className="main-area">
      <div className="shell-header"><span className="brand-tile"><BrandMark size={24} /></span></div>
      {signOutError(false, 'shell-error')}
      {children}
    </div>
    <nav className="tab-bar" aria-label={messages.navigationLabel}>
      {nav.map((item) => <a
        key={item.id}
        className={active === item.id ? 'tab-link active' : 'tab-link'}
        href={item.href}
        aria-current={active === item.id ? 'page' : undefined}
      >{item.id === 'add'
        ? <span className="tab-add-icon"><Icon name={item.icon} size={18} /></span>
        : <Icon name={item.icon} size={22} />}{item.label}</a>)}
      <button className="tab-link" type="button" onClick={() => void signOut()} aria-label={messages.signOut}><Icon name="logout" size={22} /></button>
    </nav>
  </div>;
}
