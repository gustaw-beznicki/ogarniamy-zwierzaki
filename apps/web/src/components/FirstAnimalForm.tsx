import { useState } from 'react';
import type { Locale, Messages } from '../i18n';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api } from '../lib/api';
import { AnimalNameForm } from './AnimalNameForm';
import { BrandMark } from './Icon';

// A 4xx answer means the API rejected the create; anything else (no response, a server error) leaves it unknown
// whether the animal was saved.
function createOutcomeUnknown(error: unknown): boolean {
  return !(error instanceof ApiError) || error.status === 0 || error.status >= 500;
}

export function FirstAnimalForm({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [name, setName] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [createUncertain, setCreateUncertain] = useState(false);
  const [checking, setChecking] = useState(false);
  const animalsPath = clientLocalePath(locale, 'animals');
  async function submit(trimmed: string) {
    setError('');
    setBusy(true);
    try {
      await api.createAnimal(trimmed);
      // busy stays set while the animals page opens so the form cannot be submitted twice.
      window.location.assign(animalsPath);
    } catch (cause) {
      setBusy(false);
      if (createOutcomeUnknown(cause)) setCreateUncertain(true);
      else setError(cause instanceof ApiError ? cause.code : 'request_failed');
    }
  }
  // An uncertain create is resolved by checking for a saved animal; the draft is kept for an explicit resubmit.
  async function checkAfterUncertainCreate() {
    setChecking(true);
    try {
      if ((await api.animals(true)).length > 0) {
        window.location.assign(animalsPath);
        return;
      }
      setCreateUncertain(false);
    } catch {
      // Still unknown: the notice and the refresh button stay.
    }
    setChecking(false);
  }
  return <main className="content"><section className="card form-card onboarding-card">
    <span className="brand-tile brand-tile-lg"><BrandMark size={36} /></span>
    <h1>{messages.firstAnimalTitle}</h1><p>{messages.firstAnimalIntro}</p>
    <AnimalNameForm messages={messages} value={name} onChange={setName} onSubmit={(trimmed) => void submit(trimmed)} submitLabel={messages.saveAndContinue} busy={busy} blocked={createUncertain || checking} error={error}>
      {createUncertain && <div className="notice" role="alert">
        <p>{messages.createUncertain}</p>
        <button type="button" className="secondary" disabled={checking} onClick={() => void checkAfterUncertainCreate()}>{checking ? messages.loadingContent : messages.refreshList}</button>
      </div>}
    </AnimalNameForm>
  </section></main>;
}
