import { useState, type FormEvent } from 'react';
import type { Locale, Messages } from '../i18n';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api } from '../lib/api';

export function AuthForm({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    setBusy(true);
    const data = new FormData(event.currentTarget);
    try {
      const email = String(data.get('email') ?? '').trim();
      const password = String(data.get('password') ?? '');
      if (mode === 'register') await api.register(email, password);
      else await api.login(email, password);
      window.location.assign(clientLocalePath(locale));
    } catch (cause) {
      const code = cause instanceof ApiError ? cause.code : 'authError';
      setError(code in messages ? messages[code as keyof Messages] : messages.authError);
    } finally {
      setBusy(false);
    }
  }
  return <main className="content auth-page"><section className="card auth-card">
    <h1>{messages.appName}</h1>
    <div className="auth-tabs" role="tablist" aria-label={messages.appName}>
      <button type="button" role="tab" aria-selected={mode === 'login'} className={mode === 'login' ? 'selected' : ''} onClick={() => { setMode('login'); setError(''); }}>{messages.signIn}</button>
      <button type="button" role="tab" aria-selected={mode === 'register'} className={mode === 'register' ? 'selected' : ''} onClick={() => { setMode('register'); setError(''); }}>{messages.createAccount}</button>
    </div>
    <form onSubmit={submit}>
      <label>{messages.email}<input name="email" type="email" autoComplete="email" placeholder={messages.emailPlaceholder} required /></label>
      <label>{messages.password}<input name="password" type="password" autoComplete={mode === 'login' ? 'current-password' : 'new-password'} placeholder={messages.passwordPlaceholder} minLength={mode === 'register' ? 10 : undefined} required /></label>
      {error && <p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {error}</p>}
      <button className="primary" type="submit" disabled={busy}>{busy ? (mode === 'login' ? messages.signingIn : messages.creatingAccount) : (mode === 'login' ? messages.signIn : messages.createAccount)}</button>
    </form>
  </section></main>;
}
