import { useEffect, useState } from 'react';
import type { Locale, Messages } from '../i18n';
import { formatEventDate, formatTimestamp, interpolate, uuidQueryParameter } from '../i18n/format';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api, type DocumentReadingState, type StoredDocument } from '../lib/api';
import { DocumentReadingStatus } from './DocumentReadingStatus';
import { Icon } from './Icon';
import { PageState } from './PageState';

type LoadState = 'loading' | 'ready' | 'unavailable' | 'failed';

// Originals are only ever served by the same-origin API; any other URL is refused rather than rendered.
const originalUrlPrefix = '/api/documents/';

function downloadUrl(originalUrl: string): string {
  return `${originalUrl}${originalUrl.includes('?') ? '&' : '?'}download=true`;
}

export function DocumentDetails({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [state, setState] = useState<LoadState>('loading');
  const [document, setDocument] = useState<StoredDocument | null>(null);
  // The page shown in the viewer; it starts at the first page whenever a document is loaded.
  const [selectedPage, setSelectedPage] = useState(0);
  // Reported by the reading panel; only drives the decorative scan line over the viewer.
  const [readingState, setReadingState] = useState<DocumentReadingState | null>(null);

  useEffect(() => {
    let active = true;
    const id = uuidQueryParameter('id');
    if (!id) {
      setState('unavailable');
      return;
    }
    void api.document(id).then((stored) => {
      if (!active) return;
      if (!stored.files.every((file) => file.originalUrl.startsWith(originalUrlPrefix))) {
        setState('failed');
        return;
      }
      setDocument(stored);
      setSelectedPage(0);
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

  const backToAnimals = <a className="back-link" href={clientLocalePath(locale, 'animals')}>
    <span className="back-bubble" aria-hidden="true"><Icon name="chevronLeft" size={20} /></span>{messages.backToAnimals}
  </a>;

  if (state === 'loading') return <main className="content"><p className="loading" role="status">{messages.loadingContent}</p></main>;
  if (state !== 'ready' || !document) {
    return <main className="content">
      <PageState tone="error" headingLevel={1} title={state === 'failed' ? messages.request_failed : messages.documentUnavailable} action={backToAnimals} />
    </main>;
  }

  const files = [...document.files].sort((a, b) => a.position - b.position);
  const pdf = files.length === 1 && files[0]?.contentType === 'application/pdf' ? files[0] : null;
  const pageIndex = Math.min(selectedPage, files.length - 1);
  const page = files[pageIndex];
  const pageLabel = interpolate(messages.pageLabel, { position: pageIndex + 1, total: files.length });
  return <main className="content"><section className="document-page">
    <a className="back-link" href={`${clientLocalePath(locale, 'animal-documents')}?animalId=${encodeURIComponent(document.animalId)}`}>
      <span className="back-bubble" aria-hidden="true"><Icon name="chevronLeft" size={20} /></span>{messages.backToDocuments}
    </a>
    <div className="document-heading">
      <p className="document-animal">{document.animalName}</p>
      <h1>{messages.documentTitle}</h1>
    </div>
    <div className="document-layout">
      <div className="document-side">
        <dl className="document-fields">
          <div className="document-field">
            <dt><span className="document-field-icon" aria-hidden="true"><Icon name="calendar" size={16} /></span>{messages.eventDate}</dt>
            <dd>{formatEventDate(locale, document.eventDate)}</dd>
          </div>
          <div className="document-field">
            <dt><span className="document-field-icon" aria-hidden="true"><Icon name="upload" size={16} /></span>{messages.uploadedOn}</dt>
            <dd>{formatTimestamp(locale, document.uploadedAt)}</dd>
          </div>
        </dl>
        <p className="stored-pill"><Icon name="shieldCheck" />{messages.storedNotice}</p>
        {/* Fetched and polled on its own: a status failure never hides the details or the originals. */}
        <DocumentReadingStatus documentId={document.id} locale={locale} messages={messages} onStateChange={setReadingState} />
      </div>
      <section className="document-originals" aria-labelledby="originals-title">
        <h2 id="originals-title">{messages.originals}</h2>
        {pdf
          ? <div className="card pdf-original">
            <div className="pdf-file">
              <span className="tile file-pdf" aria-hidden="true"><Icon name="pdf" /></span>
              <p className="file-name">{pdf.originalName}</p>
            </div>
            <div className="original-actions">
              <a className="primary" href={pdf.originalUrl} target="_blank" rel="noopener"><Icon name="external" size={18} />{messages.openPdf}</a>
              <a className="secondary" href={downloadUrl(pdf.originalUrl)}><Icon name="download" size={18} />{messages.downloadPdf}</a>
            </div>
          </div>
          : page && <>
            <figure className="viewer">
              <div className="viewer-stage">
                <img src={page.originalUrl} alt={`${pageLabel}: ${page.originalName}`} />
                {readingState === 'reading' && <span className="viewer-scan" aria-hidden="true" />}
              </div>
              <figcaption>{pageLabel}</figcaption>
            </figure>
            <div className="viewer-controls">
              {files.length > 1 && <div className="viewer-thumbs" role="group" aria-label={messages.originalPagesLabel}>
                {files.map((file, index) => <button
                  key={file.id}
                  type="button"
                  className="viewer-thumb"
                  aria-label={interpolate(messages.pageLabel, { position: index + 1, total: files.length })}
                  aria-current={index === pageIndex ? 'true' : undefined}
                  onClick={() => setSelectedPage(index)}
                ><img src={file.originalUrl} alt="" loading="lazy" /></button>)}
              </div>}
              <div className="original-actions">
                <a className="primary" href={page.originalUrl} target="_blank" rel="noopener" aria-label={`${messages.openOriginal}: ${pageLabel}`}><Icon name="external" size={18} />{messages.openOriginal}</a>
                <a className="secondary" href={downloadUrl(page.originalUrl)} aria-label={`${messages.downloadOriginal}: ${pageLabel}`}><Icon name="download" size={18} />{messages.downloadOriginal}</a>
              </div>
            </div>
          </>}
      </section>
    </div>
  </section></main>;
}
