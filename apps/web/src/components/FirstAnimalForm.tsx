import { useState } from 'react';
import type { Locale, Messages } from '../i18n';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api } from '../lib/api';
import { AnimalNameForm } from './AnimalNameForm';

export function FirstAnimalForm({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [name, setName] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function submit(trimmed: string) {
    setError('');
    setBusy(true);
    try {
      await api.createAnimal(trimmed);
      window.location.assign(clientLocalePath(locale, 'animals'));
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.code : 'request_failed');
    } finally { setBusy(false); }
  }
  return <main className="content"><section className="card form-card">
    <h1>{messages.firstAnimalTitle}</h1><p>{messages.firstAnimalIntro}</p>
    <AnimalNameForm messages={messages} value={name} onChange={setName} onSubmit={(trimmed) => void submit(trimmed)} submitLabel={messages.saveAndContinue} busy={busy} error={error} />
  </section></main>;
}
