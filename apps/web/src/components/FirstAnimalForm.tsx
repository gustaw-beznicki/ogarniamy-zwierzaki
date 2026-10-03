import { useState, type FormEvent } from 'react';
import type { Locale, Messages } from '../i18n';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api } from '../lib/api';

export function FirstAnimalForm({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    const form = event.currentTarget;
    const name = String(new FormData(form).get('name') ?? '').trim();
    if (!name) { setError('name_required'); return; }
    if (name.length > 100) { setError('name_too_long'); return; }
    setBusy(true);
    try {
      await api.createAnimal(name);
      window.location.assign(clientLocalePath(locale, 'animals'));
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.code : 'request_failed');
    } finally { setBusy(false); }
  }
  return <main className="content"><section className="card form-card">
    <h1>{messages.firstAnimalTitle}</h1><p>{messages.firstAnimalIntro}</p>
    <form onSubmit={submit}>
      <label>{messages.animalName}<input name="name" maxLength={100} placeholder={messages.animalNamePlaceholder} required /></label>
      {error && <p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {messages[error as keyof Messages] ?? messages.request_failed}</p>}
      <button className="primary" type="submit" disabled={busy}>{busy ? messages.saving : messages.saveAndContinue}</button>
    </form>
  </section></main>;
}
