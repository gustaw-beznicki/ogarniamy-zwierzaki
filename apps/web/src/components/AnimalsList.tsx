import { useEffect, useRef, useState } from 'react';
import type { Locale, Messages } from '../i18n';
import { countLabel, interpolate } from '../i18n/format';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api, type Animal } from '../lib/api';
import { AnimalNameForm } from './AnimalNameForm';

function profilePath(locale: Locale, id: string): string {
  return `${clientLocalePath(locale, 'animal-documents')}?animalId=${encodeURIComponent(id)}`;
}

// A 4xx answer other than an expired session means the API rejected the create; anything else (no response, a server
// error) leaves it unknown whether the animal was saved.
function createOutcomeUnknown(error: unknown): boolean {
  return !(error instanceof ApiError) || error.status === 0 || error.status >= 500;
}

function AnimalRow({ locale, messages, animal }: { locale: Locale; messages: Messages; animal: Animal }) {
  const count = countLabel(locale, messages, 'documentCount', animal.storedDocumentCount);
  const label = [interpolate(messages.viewDocumentsLabel, { name: animal.name }), count, animal.isActive ? null : messages.statusInactive]
    .filter(Boolean).join(', ');
  return <li><a className="animal-link" href={profilePath(locale, animal.id)} aria-label={label}>
    <span className="paw" aria-hidden="true">♧</span>
    <span className="animal-meta"><span>{animal.name}</span><span className="muted">{count}</span></span>
    {!animal.isActive && <span className="status-pill inactive" aria-hidden="true">{messages.statusInactive}</span>}
    <span className="chevron" aria-hidden="true">›</span>
  </a></li>;
}

export function AnimalsList({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [animals, setAnimals] = useState<Animal[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState('');
  const [busy, setBusy] = useState(false);
  const [createError, setCreateError] = useState('');
  const [createUncertain, setCreateUncertain] = useState(false);
  const addButton = useRef<HTMLButtonElement>(null);

  function redirectIfSignedOut(error: unknown): boolean {
    if (error instanceof ApiError && error.code === 'unauthorized') {
      window.location.replace(clientLocalePath(locale, 'signin'));
      return true;
    }
    return false;
  }

  async function load() {
    setLoading(true);
    setFailed(false);
    try {
      setAnimals(await api.animals(true));
      return true;
    } catch (error) {
      if (!redirectIfSignedOut(error)) setFailed(true);
      return false;
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void load(); }, [locale]);

  function closeForm() {
    setAdding(false);
    setCreateError('');
    setCreateUncertain(false);
    // The button is rendered again on the next frame, so focus returns to it afterwards.
    window.requestAnimationFrame(() => addButton.current?.focus());
  }

  async function create(name: string) {
    setBusy(true);
    setCreateError('');
    setCreateUncertain(false);
    try {
      const created = await api.createAnimal(name);
      // busy stays set while the profile page opens so the form cannot be submitted twice.
      window.location.assign(profilePath(locale, created.id));
    } catch (error) {
      setBusy(false);
      if (redirectIfSignedOut(error)) return;
      if (createOutcomeUnknown(error)) {
        setCreateUncertain(true);
      } else {
        setCreateError(error instanceof ApiError ? error.code : 'request_failed');
      }
    }
  }

  // The uncertain create is resolved only by looking at the current list; the draft is kept for an explicit resubmit.
  async function refreshAfterUncertainCreate() {
    if (await load()) setCreateUncertain(false);
  }

  const active = animals?.filter((animal) => animal.isActive) ?? [];
  const inactive = animals?.filter((animal) => !animal.isActive) ?? [];

  return <main className="content"><section className="animal-section">
    <div className="section-header">
      <h1>{messages.animalsTitle}</h1>
      {!adding && <button ref={addButton} type="button" className="primary" onClick={() => setAdding(true)}>{messages.addAnimal}</button>}
    </div>

    {adding && <section className="card add-animal" aria-labelledby="add-animal-title">
      <h2 id="add-animal-title">{messages.addAnimalTitle}</h2>
      <AnimalNameForm
        messages={messages}
        value={draft}
        onChange={setDraft}
        onSubmit={(name) => void create(name)}
        submitLabel={messages.addAnimal}
        busy={busy}
        blocked={createUncertain || loading}
        error={createError}
        onCancel={closeForm}
        autoFocus
      >
        {createUncertain && <div className="notice" role="alert">
          <p>{messages.createUncertain}</p>
          <button type="button" className="secondary" disabled={loading} onClick={() => void refreshAfterUncertainCreate()}>{loading ? messages.loadingContent : messages.refreshList}</button>
        </div>}
      </AnimalNameForm>
    </section>}

    {failed && <div className="list-error">
      <p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {messages.request_failed}</p>
      <button type="button" className="secondary" disabled={loading} onClick={() => void load()}>{messages.retry}</button>
    </div>}
    {animals === null && loading ? <p className="loading" role="status">{messages.loadingContent}</p> : null}

    {animals && animals.length === 0 && <p className="notice">{messages.noAnimals}</p>}
    {animals && animals.length > 0 && active.length === 0 && <p className="notice">{messages.noActiveAnimalsManage}</p>}
    {active.length > 0 && <ul className="animal-list">{active.map((animal) => <AnimalRow key={animal.id} locale={locale} messages={messages} animal={animal} />)}</ul>}

    {inactive.length > 0 && <details className="inactive-section">
      <summary>{interpolate(messages.inactiveSection, { count: inactive.length })}</summary>
      <ul className="animal-list">{inactive.map((animal) => <AnimalRow key={animal.id} locale={locale} messages={messages} animal={animal} />)}</ul>
    </details>}
  </section></main>;
}
