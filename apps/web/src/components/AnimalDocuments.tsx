import { useEffect, useRef, useState, type CSSProperties, type RefObject } from 'react';
import type { Locale, Messages } from '../i18n';
import { countLabel, formatEventDate, formatEventMonth, formatTimestamp, interpolate, uuidQueryParameter } from '../i18n/format';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api, type Animal, type DocumentSummary } from '../lib/api';
import { Alert } from './Alert';
import { AnimalAvatar } from './AnimalAvatar';
import { AnimalNameForm, errorMessage } from './AnimalNameForm';
import { BrandMark, Icon } from './Icon';
import { PageState } from './PageState';

const pageSize = 20;

type MonthGroup = { key: string; documents: DocumentSummary[] };

// Groups the already-ordered history by the yyyy-MM of its event date without re-sorting. Rows appended by
// "Show more" for a month already shown join that month's group, so each distinct month has one heading.
function groupByMonth(documents: DocumentSummary[]): MonthGroup[] {
  const groups: MonthGroup[] = [];
  const byKey = new Map<string, MonthGroup>();
  for (const document of documents) {
    const key = document.eventDate.slice(0, 7);
    let group = byKey.get(key);
    if (!group) {
      group = { key, documents: [] };
      byKey.set(key, group);
      groups.push(group);
    }
    group.documents.push(document);
  }
  return groups;
}

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

  const backToAnimals = <a className="back-link" href={clientLocalePath(locale, 'animals')}>
    <span className="back-bubble" aria-hidden="true"><Icon name="chevronLeft" size={20} /></span>{messages.backToAnimals}
  </a>;

  if (state === 'loading') return <main className="content"><p className="loading" role="status">{messages.loadingContent}</p></main>;
  if (state === 'accountChanged') {
    return <main className="content"><Alert variant="warning" role="alert">{messages.readingAccountChanged}</Alert></main>;
  }
  if (state === 'unavailable' || state === 'failed' || !animal) {
    return <main className="content">
      <PageState tone="error" headingLevel={1} title={state === 'failed' ? messages.request_failed : messages.documentsUnavailable} action={backToAnimals} />
    </main>;
  }

  const conflictNotice = (text: string) => <Alert
    variant="warning"
    role="alert"
    action={<button type="button" disabled={refreshing} onClick={() => void refreshProfile()}>{refreshing ? messages.loadingContent : messages.refresh}</button>}
  >{text}</Alert>;
  const errorText = mutationError ? <Alert variant="error">{errorMessage(messages, mutationError)}</Alert> : null;
  const groups = groupByMonth(documents);
  let rowIndex = 0;

  return <main className="content"><section className="documents-section">
    {backToAnimals}
    <section className={animal.isActive ? 'profile-hero' : 'profile-hero inactive'} aria-labelledby="profile-name">
      {animal.isActive && <BrandMark size={170} className="profile-hero-paw" />}
      <div className="profile-hero-head">
        <AnimalAvatar animal={animal} locale={locale} size="hero" />
        <div className="profile-hero-text">
          <h1 id="profile-name">{animal.name}</h1>
          <div className="profile-hero-meta">
            <span className="status-pill"><span className="status-dot" aria-hidden="true" />{animal.isActive ? messages.statusActive : messages.statusInactive}</span>
            <span className="profile-hero-count">{countLabel(locale, messages, 'documentCount', animal.storedDocumentCount)}</span>
          </div>
        </div>
      </div>
      {mode === 'idle' && <div className="profile-actions">
        <button ref={editButton} type="button" className="glass-button" disabled={busy} onClick={startRename}><Icon name="pencil" size={18} />{messages.editName}</button>
        {animal.isActive && <button ref={activityButton} type="button" className="glass-button" disabled={busy} onClick={startInactivate}><Icon name="archive" size={18} />{messages.markInactive}</button>}
      </div>}
    </section>
    {!animal.isActive && <Alert
      variant="warning"
      action={mode === 'idle'
        ? <button ref={activityButton} type="button" disabled={busy || conflict === 'activity'} onClick={() => void changeActivity(true, animal.version)}>{busy ? messages.saving : messages.markActive}</button>
        : undefined}
    >{messages.inactiveProfileNotice}</Alert>}
    {/* Persistent live region: the Alert inside it is not a live region itself. */}
    <div className="profile-status" role="status">{notice
      ? <Alert variant={notice === messages.profileRefreshedActivity ? 'info' : 'success'} compact role={null}>{notice}</Alert>
      : null}</div>

    {mode === 'idle' && <div className="profile-messages">
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
        {refreshedDraft && <Alert variant="info" compact role={null}>{interpolate(messages.profileRefreshedDraft, { name: animal.name })}</Alert>}
      </AnimalNameForm>
    </section>}

    {mode === 'confirmInactive' && <section className="card profile-edit profile-confirm" role="group" aria-labelledby="inactivate-title" aria-describedby="inactivate-body" tabIndex={-1} autoFocus>
      <span className="tile tile-warning" aria-hidden="true"><Icon name="archive" /></span>
      <h2 id="inactivate-title">{interpolate(messages.confirmInactiveTitle, { name: animal.name })}</h2>
      <p id="inactivate-body">{messages.confirmInactiveBody}</p>
      {conflict === 'activity' && conflictNotice(messages.activityChanged)}
      {errorText}
      <div className="form-actions confirm-actions">
        <button type="button" className="secondary" disabled={busy} onClick={() => cancel(activityButton)}>{messages.cancel}</button>
        <button type="button" className="primary" disabled={busy || conflict === 'activity'} onClick={() => void changeActivity(false, editVersion)}>{busy ? messages.saving : messages.markInactive}</button>
      </div>
    </section>}

    <div className="history-header">
      <div>
        <h2 className="history-title">{messages.documentsTitle}</h2>
        <p className="hint">{messages.documentsOrder}</p>
      </div>
      {animal.isActive && <a className="accent history-add" href={`${clientLocalePath(locale, 'add')}?animalId=${encodeURIComponent(animal.id)}`}><Icon name="plus" size={18} />{messages.addDocument}</a>}
    </div>
    {documents.length === 0
      ? <Alert variant="info">{messages.documentsEmpty}</Alert>
      : <div className="timeline">
        <span className="timeline-rail" aria-hidden="true" />
        {groups.map((group) => <div key={group.key} className="timeline-group">
          <h3 className="timeline-month">{formatEventMonth(locale, group.documents[0].eventDate)}</h3>
          <ul className="document-list">{group.documents.map((document) => {
            const date = formatEventDate(locale, document.eventDate);
            const files = countLabel(locale, messages, 'fileCount', document.fileCount);
            const style = { '--i': Math.min(rowIndex++, 10) } as CSSProperties;
            return <li key={document.id} className="timeline-item rise" style={style}>
              <span className="timeline-dot" aria-hidden="true" />
              <a className="document-link" href={`${clientLocalePath(locale, 'document')}?id=${encodeURIComponent(document.id)}`} aria-label={`${interpolate(messages.openDocumentLabel, { date })}, ${files}`}>
                <span className="document-tile" aria-hidden="true"><Icon name="document" /></span>
                <span className="document-meta">
                  <span className="document-date">{date}</span>
                  <span className="muted">{files}</span>
                  <span className="muted">{`${messages.uploadedOn}: ${formatTimestamp(locale, document.uploadedAt)}`}</span>
                </span>
                <Icon name="chevronRight" size={20} className="document-chevron" />
              </a>
            </li>;
          })}</ul>
        </div>)}
      </div>}
    {moreFailed && <Alert variant="error">{messages.request_failed}</Alert>}
    {hasMore && <button type="button" className="show-more" disabled={loadingMore} onClick={() => void loadMore()}>
      {loadingMore ? messages.loadingContent : messages.showMore}<Icon name="chevronDown" size={18} />
    </button>}
  </section></main>;
}
