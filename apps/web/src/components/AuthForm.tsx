import { useId, useState, type FormEvent } from 'react';
import type { Locale, Messages } from '../i18n';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api } from '../lib/api';
import { Alert, FieldError } from './Alert';
import { BrandMark, Icon } from './Icon';

type AuthField = 'email' | 'password';

// The field a server error code concerns, or null when it belongs to the whole form. Presentation only: it decides
// where the message renders, never which message is shown.
export function authErrorField(code: string): AuthField | null {
  if (code === 'invalid_email' || code === 'email_taken') return 'email';
  if (code === 'password_too_short') return 'password';
  return null;
}

export function AuthForm({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [error, setError] = useState<{ text: string; field: AuthField | null } | null>(null);
  const [busy, setBusy] = useState(false);
  const emailErrorId = useId();
  const passwordErrorId = useId();
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
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
      setError({ text: code in messages ? messages[code as keyof Messages] : messages.authError, field: authErrorField(code) });
    } finally {
      setBusy(false);
    }
  }
  const emailError = error?.field === 'email' ? error.text : '';
  const passwordError = error?.field === 'password' ? error.text : '';
  const formError = error && !error.field ? error.text : '';
  return <main className="auth-page">
    <div className="auth-hero">
      <BrandMark className="auth-paw auth-paw-1" size={64} />
      <BrandMark className="auth-paw auth-paw-2" size={44} />
      <BrandMark className="auth-paw auth-paw-3" size={88} />
      <div className="auth-hero-inner">
        <span className="brand-tile brand-tile-accent brand-tile-lg"><BrandMark size={36} /></span>
        <h1>{messages.appName}</h1>
        <p className="auth-tagline">{messages.authTagline}</p>
      </div>
    </div>
    <section className="card auth-card">
      <div className={mode === 'register' ? 'auth-tabs register' : 'auth-tabs'} role="tablist" aria-label={messages.appName}>
        <span className="auth-tabs-indicator" aria-hidden="true" />
        <button type="button" role="tab" aria-selected={mode === 'login'} className={mode === 'login' ? 'selected' : ''} onClick={() => { setMode('login'); setError(null); }}>{messages.signIn}</button>
        <button type="button" role="tab" aria-selected={mode === 'register'} className={mode === 'register' ? 'selected' : ''} onClick={() => { setMode('register'); setError(null); }}>{messages.createAccount}</button>
      </div>
      <form onSubmit={submit}>
        <div className="field-group">
          <label>{messages.email}<span className={emailError ? 'field invalid' : 'field'}><Icon name="mail" size={22} /><input
            name="email"
            type="email"
            autoComplete="email"
            placeholder={messages.emailPlaceholder}
            required
            aria-invalid={emailError ? true : undefined}
            aria-describedby={emailError ? emailErrorId : undefined}
          /></span></label>
          <div className="field-error-slot" role="alert">{emailError && <FieldError id={emailErrorId}>{emailError}</FieldError>}</div>
        </div>
        <div className="field-group">
          <label>{messages.password}<span className={passwordError ? 'field invalid' : 'field'}><Icon name="lock" size={22} /><input
            name="password"
            type="password"
            autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            placeholder={messages.passwordPlaceholder}
            minLength={mode === 'register' ? 10 : undefined}
            required
            aria-invalid={passwordError ? true : undefined}
            aria-describedby={passwordError ? passwordErrorId : undefined}
          /></span></label>
          <div className="field-error-slot" role="alert">{passwordError && <FieldError id={passwordErrorId}>{passwordError}</FieldError>}</div>
        </div>
        {formError && <Alert variant="error">{formError}</Alert>}
        <button className="primary auth-submit" type="submit" disabled={busy}>{busy ? (mode === 'login' ? messages.signingIn : messages.creatingAccount) : (mode === 'login' ? messages.signIn : messages.createAccount)}<Icon name="arrowRight" size={22} className="auth-submit-arrow" /></button>
      </form>
    </section>
    <p className="auth-privacy"><Icon name="shieldCheck" size={20} />{messages.authPrivacyNote}</p>
  </main>;
}
