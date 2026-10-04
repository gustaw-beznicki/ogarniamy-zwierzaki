import { useEffect, useState } from 'react';
import type { Locale, Messages } from '../i18n';
import { countLabel, formatEventDate, interpolate, uuidQueryParameter } from '../i18n/format';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api, type Animal, type DocumentSummary } from '../lib/api';

const pageSize = 20;

type LoadState = 'loading' | 'ready' | 'unavailable' | 'failed';

export function AnimalDocuments({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [state, setState] = useState<LoadState>('loading');
  const [animal, setAnimal] = useState<Animal | null>(null);
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [moreFailed, setMoreFailed] = useState(false);

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
    void Promise.all([api.animal(animalId), api.animalDocuments(animalId, 0, pageSize)]).then(([owned, page]) => {
      if (!active) return;
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

  const backToAnimals = <a className="back-link" href={clientLocalePath(locale, 'animals')}><span aria-hidden="true">‹</span> {messages.backToAnimals}</a>;

  if (state === 'loading') return <main className="content"><p className="loading" role="status">{messages.loadingContent}</p></main>;
  if (state === 'unavailable' || state === 'failed' || !animal) {
    return <main className="content">{backToAnimals}<p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {state === 'failed' ? messages.request_failed : messages.documentsUnavailable}</p></main>;
  }

  return <main className="content"><section className="documents-section">
    {backToAnimals}
    <div className="section-header">
      <div><p className="eyebrow">{messages.documentsTitle}</p><h1>{animal.name}</h1></div>
      <a className="primary button-link" href={`${clientLocalePath(locale, 'add')}?animalId=${encodeURIComponent(animal.id)}`}>{messages.addDocument}</a>
    </div>
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
