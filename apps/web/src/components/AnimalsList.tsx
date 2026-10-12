import { useEffect, useRef, useState, type CSSProperties } from 'react';
import type { Locale, Messages } from '../i18n';
import { countLabel, interpolate } from '../i18n/format';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api, type Animal } from '../lib/api';
import { Alert } from './Alert';
import { AnimalAvatar } from './AnimalAvatar';
import { AnimalNameForm } from './AnimalNameForm';
import { Icon } from './Icon';

function profilePath(locale: Locale, id: string): string {
  return `${clientLocalePath(locale, 'animal-documents')}?animalId=${encodeURIComponent(id)}`;
}

// A 4xx answer other than an expired session means the API rejected the create; anything else (no response, a server
// error) leaves it unknown whether the animal was saved.
function createOutcomeUnknown(error: unknown): boolean {
  return !(error instanceof ApiError) || error.status === 0 || error.status >= 500;
}

// Entrance stagger step for list rows (.rise); capped so long lists do not wait.
function riseStyle(index: number): CSSProperties {
  return { '--i': Math.min(index, 10) } as CSSProperties;
}

// A row below 768 px; active rows become cards from 768 px (the avatar's card size and the arrow apply there only).
function AnimalRow({ locale, messages, animal, index }: { locale: Locale; messages: Messages; animal: Animal; index: number }) {
  const count = countLabel(locale, messages, 'documentCount', animal.storedDocumentCount);
  const label = [interpolate(messages.viewDocumentsLabel, { name: animal.name }), count, animal.isActive ? null : messages.statusInactive]
    .filter(Boolean).join(', ');
  return <li className="rise" style={riseStyle(index)}><a className={animal.isActive ? 'animal-link' : 'animal-link inactive'} href={profilePath(locale, animal.id)} aria-label={label}>
    <AnimalAvatar animal={animal} locale={locale} size={animal.isActive ? 'card' : 'row'} />
    <span className="animal-meta"><span className="animal-name">{animal.name}</span><span className="muted">{count}</span></span>
    {animal.isActive
      ? <span className="animal-go" aria-hidden="true"><Icon name="chevronRight" size={20} className="go-row" /><Icon name="arrowRight" size={20} className="go-card" /></span>
      : <span className="status-pill" aria-hidden="true">{messages.statusInactive}</span>}
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
      {!adding && <button ref={addButton} type="button" className="add-animal-button" onClick={() => setAdding(true)}>
        <span className="add-animal-plus" aria-hidden="true"><Icon name="plus" size={18} /></span>{messages.addAnimal}
      </button>}
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
        {createUncertain && <Alert
          variant="warning"
          role="alert"
          action={<button type="button" disabled={loading} onClick={() => void refreshAfterUncertainCreate()}>{loading ? messages.loadingContent : messages.refreshList}</button>}
        >{messages.createUncertain}</Alert>}
      </AnimalNameForm>
    </section>}

    {failed && <Alert
      variant="error"
      action={<button type="button" disabled={loading} onClick={() => void load()}>{messages.retry}</button>}
    >{messages.request_failed}</Alert>}
    {animals === null && loading ? <p className="loading" role="status">{messages.loadingContent}</p> : null}

    {animals && animals.length === 0 && <Alert variant="info">{messages.noAnimals}</Alert>}
    {animals && animals.length > 0 && active.length === 0 && <Alert variant="info">{messages.noActiveAnimalsManage}</Alert>}
    {active.length > 0 && <ul className="animal-list active-animals">{active.map((animal, index) => <AnimalRow key={animal.id} locale={locale} messages={messages} animal={animal} index={index} />)}</ul>}

    {inactive.length > 0 && <details className="inactive-section">
      <summary>
        <Icon name="archive" size={20} />
        <span className="inactive-summary-label">{interpolate(messages.inactiveSection, { count: inactive.length })}</span>
        <Icon name="chevronDown" size={20} className="inactive-chevron" />
      </summary>
      <ul className="animal-list inactive-list">{inactive.map((animal, index) => <AnimalRow key={animal.id} locale={locale} messages={messages} animal={animal} index={index} />)}</ul>
    </details>}
  </section></main>;
}
