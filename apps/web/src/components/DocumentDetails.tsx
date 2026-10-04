import { useEffect, useState } from 'react';
import type { Locale, Messages } from '../i18n';
import { formatEventDate, formatTimestamp, interpolate, uuidQueryParameter } from '../i18n/format';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api, type StoredDocument } from '../lib/api';

type LoadState = 'loading' | 'ready' | 'unavailable' | 'failed';

function downloadUrl(originalUrl: string): string {
  return `${originalUrl}${originalUrl.includes('?') ? '&' : '?'}download=true`;
}

export function DocumentDetails({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [state, setState] = useState<LoadState>('loading');
  const [document, setDocument] = useState<StoredDocument | null>(null);

  useEffect(() => {
    let active = true;
    const id = uuidQueryParameter('id');
    if (!id) {
      setState('unavailable');
      return;
    }
    void api.document(id).then((stored) => {
      if (!active) return;
      setDocument(stored);
      setState('ready');
    }).catch((error: unknown) => {
      if (!active) return;
      if (error instanceof ApiError && error.code === 'unauthorized') {
        window.location.replace(clientLocalePath(locale, 'signin'));
        return;
      }
      // A missing and another account's document are indistinguishable.
      setState(error instanceof ApiError && error.status === 404 ? 'unavailable' : 'failed');
    });
    return () => { active = false; };
  }, [locale]);

  if (state === 'loading') return <main className="content"><p className="loading" role="status">{messages.loadingContent}</p></main>;
  if (state !== 'ready' || !document) {
    return <main className="content"><a className="back-link" href={clientLocalePath(locale, 'animals')}><span aria-hidden="true">‹</span> {messages.backToAnimals}</a><p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {state === 'failed' ? messages.request_failed : messages.documentUnavailable}</p></main>;
  }

  const files = [...document.files].sort((a, b) => a.position - b.position);
  const pdf = files.length === 1 && files[0]?.contentType === 'application/pdf' ? files[0] : null;
  return <main className="content"><section className="document-section">
    <a className="back-link" href={`${clientLocalePath(locale, 'animal-documents')}?animalId=${encodeURIComponent(document.animalId)}`}><span aria-hidden="true">‹</span> {messages.backToDocuments}</a>
    <p className="eyebrow">{messages.documentTitle}</p>
    <h1>{document.animalName}</h1>
    <dl className="document-fields card">
      <div><dt>{messages.animal}</dt><dd>{document.animalName}</dd></div>
      <div><dt>{messages.eventDate}</dt><dd>{formatEventDate(locale, document.eventDate)}</dd></div>
      <div><dt>{messages.uploadedOn}</dt><dd>{formatTimestamp(locale, document.uploadedAt)}</dd></div>
    </dl>
    <p className="stored-notice"><span aria-hidden="true">✓</span> {messages.storedNotice}</p>
    <h2>{messages.originals}</h2>
    {pdf
      ? <div className="card pdf-original">
        <p className="file-name"><span className="file-icon" aria-hidden="true">▤</span> {pdf.originalName}</p>
        <div className="original-actions">
          <a className="primary button-link" href={pdf.originalUrl} target="_blank" rel="noopener">{messages.openPdf}</a>
          <a className="secondary button-link" href={downloadUrl(pdf.originalUrl)}>{messages.downloadPdf}</a>
        </div>
      </div>
      : <ol className="original-pages">{files.map((file, index) => {
        const label = interpolate(messages.pageLabel, { position: index + 1, total: files.length });
        return <li key={file.id} className="card original-page">
          <figure>
            <img src={file.originalUrl} alt={`${label}: ${file.originalName}`} loading={index === 0 ? 'eager' : 'lazy'} />
            <figcaption>{label} · {file.originalName}</figcaption>
          </figure>
          <div className="original-actions">
            <a className="secondary button-link" href={file.originalUrl} target="_blank" rel="noopener" aria-label={`${messages.openOriginal}: ${label}`}>{messages.openOriginal}</a>
            <a className="secondary button-link" href={downloadUrl(file.originalUrl)} aria-label={`${messages.downloadOriginal}: ${label}`}>{messages.downloadOriginal}</a>
          </div>
        </li>;
      })}</ol>}
  </section></main>;
}
