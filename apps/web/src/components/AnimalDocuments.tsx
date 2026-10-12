import { useEffect, useRef, useState, type RefObject } from 'react';
import type { Locale, Messages } from '../i18n';
import { countLabel, formatEventDate, interpolate, uuidQueryParameter } from '../i18n/format';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api, type Animal, type DocumentSummary } from '../lib/api';
import { AnimalNameForm, errorMessage } from './AnimalNameForm';

const pageSize = 20;

type LoadState = 'loading' | 'ready' | 'unavailable' | 'failed' | 'accountChanged';
// Only one profile edit is open at a time, so an edit never starts from a version another open edit is about to change.
type Mode = 'idle' | 'rename' | 'confirmInactive';
type Conflict = 'rename' | 'activity' | null;

export function AnimalDocuments({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [state, setState] = useState<LoadState>('loading');
  const [animal, setAnimal] = useState<Animal | null>(null);
  // The account that loaded the profile; edits are sent only on its behalf.
  const [accountEmail, setAccountEmail] = useState('');
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [moreFailed, setMoreFailed] = useState(false);

  const [mode, setMode] = useState<Mode>('idle');
  // The draft and the version it was started from; the version is replaced only by an explicit refresh.
  const [draft, setDraft] = useState('');
  const [editVersion, setEditVersion] = useState('');
  const [busy, setBusy] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [mutationError, setMutationError] = useState('');
  const [conflict, setConflict] = useState<Conflict>(null);
  const [refreshedDraft, setRefreshedDraft] = useState(false);
  const [notice, setNotice] = useState('');
  const editButton = useRef<HTMLButtonElement>(null);
  const activityButton = useRef<HTMLButtonElement>(null);

  function handleError(error: unknown): LoadState | null {
    if (error instanceof ApiError && error.code === 'unauthorized') {
      window.location.replace(clientLocalePath(locale, 'signin'));
      return null;
    }
    // A missing and another account's animal are indistinguishable.
    return error instanceof ApiError && error.status === 404 ? 'unavailable' : 'failed';
  }

  useEffect(() => {
    let active = true;
    const animalId = uuidQueryParameter('animalId');
    if (!animalId) {
      setState('unavailable');
      return;
    }
    void Promise.all([api.me(), api.animal(animalId), api.animalDocuments(animalId, 0, pageSize)]).then(([me, owned, page]) => {
      if (!active) return;
      setAccountEmail(me.email);
      setAnimal(owned);
      setDocuments(page.items);
      setHasMore(page.hasMore);
      setState('ready');
    }).catch((error: unknown) => {
      if (!active) return;
      const next = handleError(error);
      if (next) setState(next);
    });
    return () => { active = false; };
  }, [locale]);

  async function loadMore() {
    if (!animal) return;
    setLoadingMore(true);
    setMoreFailed(false);
    try {
      const page = await api.animalDocuments(animal.id, documents.length, pageSize);
      setDocuments((current) => [...current, ...page.items.filter((item) => !current.some((existing) => existing.id === item.id))]);
      setHasMore(page.hasMore);
    } catch (error) {
      if (handleError(error)) setMoreFailed(true);
    } finally {
      setLoadingMore(false);
    }
  }

  // The returning control is rendered again on the next frame, so focus moves to it afterwards.
  function focusLater(target: RefObject<HTMLButtonElement | null>) {
    window.requestAnimationFrame(() => target.current?.focus());
  }

  function resetEdit() {
    setMutationError('');
    setConflict(null);
    setRefreshedDraft(false);
  }

  function startRename() {
    if (!animal) return;
    resetEdit();
    setNotice('');
    setDraft(animal.name);
    setEditVersion(animal.version);
    setMode('rename');
  }

  function startInactivate() {
    if (!animal) return;
    resetEdit();
    setNotice('');
    setEditVersion(animal.version);
    setMode('confirmInactive');
  }

  function cancel(returnTo: RefObject<HTMLButtonElement | null>) {
    resetEdit();
    setMode('idle');
    focusLater(returnTo);
  }

  function mutationFailed(error: unknown, kind: 'rename' | 'activity') {
    if (error instanceof ApiError && error.code === 'unauthorized') {
      window.location.replace(clientLocalePath(locale, 'signin'));
    } else if (error instanceof ApiError && error.code === 'account_changed') {
      setState('accountChanged');
    } else if (error instanceof ApiError && error.code === 'animal_changed') {
      // Never replayed: the owner refreshes and then decides again.
      setConflict(kind);
    } else if (error instanceof ApiError && error.status === 404) {
      setState('unavailable');
    } else {
      setMutationError(error instanceof ApiError ? error.code : 'request_failed');
    }
  }

  async function rename(name: string) {
    if (!animal) return;
    setBusy(true);
    resetEdit();
    try {
      const updated = await api.renameAnimal(animal.id, editVersion, name, accountEmail);
      setAnimal(updated);
      setMode('idle');
      setNotice(messages.nameSaved);
      focusLater(editButton);
    } catch (error) {
      mutationFailed(error, 'rename');
    } finally {
      setBusy(false);
    }
  }

  async function changeActivity(isActive: boolean, version: string) {
    if (!animal) return;
    setBusy(true);
    resetEdit();
    setNotice('');
    try {
      const updated = await api.setAnimalActivity(animal.id, version, isActive, accountEmail);
      setAnimal(updated);
      setMode('idle');
      setNotice(updated.isActive ? messages.markedActive : messages.markedInactive);
      focusLater(activityButton);
    } catch (error) {
      mutationFailed(error, 'activity');
    } finally {
      setBusy(false);
    }
  }

  // Reloads only the profile (name, status, version); the loaded history stays as it is.
  async function refreshProfile() {
    if (!animal) return;
    setRefreshing(true);
    setMutationError('');
    try {
      const current = await api.animal(animal.id);
      setAnimal(current);
      if (conflict === 'rename') {
        // The typed draft is kept; saving it again is an explicit choice against the refreshed version.
        setEditVersion(current.version);
        setRefreshedDraft(true);
      } else {
        // An activity change is decided again from the current status, so the pending confirmation is closed.
        setMode('idle');
        setNotice(messages.profileRefreshedActivity);
        focusLater(activityButton);
      }
      setConflict(null);
    } catch (error) {
      mutationFailed(error, conflict ?? 'activity');
    } finally {
      setRefreshing(false);
    }
  }

  const backToAnimals = <a className="back-link" href={clientLocalePath(locale, 'animals')}><span aria-hidden="true">‹</span> {messages.backToAnimals}</a>;

  if (state === 'loading') return <main className="content"><p className="loading" role="status">{messages.loadingContent}</p></main>;
  if (state === 'accountChanged') {
    return <main className="content"><p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {messages.readingAccountChanged}</p></main>;
  }
  if (state === 'unavailable' || state === 'failed' || !animal) {
    return <main className="content">{backToAnimals}<p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {state === 'failed' ? messages.request_failed : messages.documentsUnavailable}</p></main>;
  }

  const conflictNotice = (text: string) => <div className="notice" role="alert">
    <p>{text}</p>
    <button type="button" className="secondary" disabled={refreshing} onClick={() => void refreshProfile()}>{refreshing ? messages.loadingContent : messages.refresh}</button>
  </div>;
  const errorText = mutationError && <p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {errorMessage(messages, mutationError)}</p>;

  return <main className="content"><section className="documents-section">
    {backToAnimals}
    <div className="section-header">
      <div>
        <p className="eyebrow">{messages.profileTitle}</p>
        <h1>{animal.name}</h1>
        <span className={animal.isActive ? 'status-pill' : 'status-pill inactive'}>{animal.isActive ? messages.statusActive : messages.statusInactive}</span>
      </div>
      {animal.isActive && <a className="primary button-link" href={`${clientLocalePath(locale, 'add')}?animalId=${encodeURIComponent(animal.id)}`}>{messages.addDocument}</a>}
    </div>
    {!animal.isActive && <p className="notice profile-notice">{messages.inactiveProfileNotice}</p>}
    <p className="profile-status" role="status">{notice}</p>

    {mode === 'idle' && <div className="profile-controls">
      <div className="profile-actions">
        <button ref={editButton} type="button" className="secondary" disabled={busy} onClick={startRename}>{messages.editName}</button>
        {animal.isActive
          ? <button ref={activityButton} type="button" className="secondary" disabled={busy} onClick={startInactivate}>{messages.markInactive}</button>
          : <button ref={activityButton} type="button" className="primary" disabled={busy || conflict === 'activity'} onClick={() => void changeActivity(true, animal.version)}>{busy ? messages.saving : messages.markActive}</button>}
      </div>
      {conflict === 'activity' && conflictNotice(messages.activityChanged)}
      {errorText}
    </div>}

    {mode === 'rename' && <section className="card profile-edit" aria-labelledby="rename-title">
      <h2 id="rename-title">{messages.editName}</h2>
      <AnimalNameForm
        messages={messages}
        value={draft}
        onChange={setDraft}
        onSubmit={(name) => void rename(name)}
        submitLabel={messages.saveName}
        busy={busy}
        blocked={conflict === 'rename' || refreshing}
        error={mutationError}
        onCancel={() => cancel(editButton)}
        autoFocus
      >
        {conflict === 'rename' && conflictNotice(messages.animal_changed)}
        {refreshedDraft && <p className="hint">{interpolate(messages.profileRefreshedDraft, { name: animal.name })}</p>}
      </AnimalNameForm>
    </section>}

    {mode === 'confirmInactive' && <section className="card profile-edit" role="group" aria-labelledby="inactivate-title" aria-describedby="inactivate-body" tabIndex={-1} autoFocus>
      <h2 id="inactivate-title">{interpolate(messages.confirmInactiveTitle, { name: animal.name })}</h2>
      <p id="inactivate-body">{messages.confirmInactiveBody}</p>
      {conflict === 'activity' && conflictNotice(messages.activityChanged)}
      {errorText}
      <div className="form-actions">
        <button type="button" className="primary" disabled={busy || conflict === 'activity'} onClick={() => void changeActivity(false, editVersion)}>{busy ? messages.saving : messages.markInactive}</button>
        <button type="button" className="secondary" disabled={busy} onClick={() => cancel(activityButton)}>{messages.cancel}</button>
      </div>
    </section>}

    <h2 className="history-title">{messages.documentsTitle}</h2>
    <p className="hint history-hint">{messages.documentsOrder}</p>
    {documents.length === 0
      ? <p className="notice">{messages.documentsEmpty}</p>
      : <ul className="document-list">{documents.map((document) => {
        const date = formatEventDate(locale, document.eventDate);
        return <li key={document.id}><a className="document-link" href={`${clientLocalePath(locale, 'document')}?id=${encodeURIComponent(document.id)}`} aria-label={`${interpolate(messages.openDocumentLabel, { date })}, ${countLabel(locale, messages, 'fileCount', document.fileCount)}`}>
          <span className="file-icon" aria-hidden="true">▤</span>
          <span className="document-meta"><span className="document-date">{date}</span><span className="muted">{messages.eventDate} · {countLabel(locale, messages, 'fileCount', document.fileCount)}</span></span>
          <span className="chevron" aria-hidden="true">›</span>
        </a></li>;
      })}</ul>}
    {moreFailed && <p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {messages.request_failed}</p>}
    {hasMore && <button type="button" className="secondary show-more" disabled={loadingMore} onClick={() => void loadMore()}>{loadingMore ? messages.loadingContent : messages.showMore}</button>}
  </section></main>;
}
