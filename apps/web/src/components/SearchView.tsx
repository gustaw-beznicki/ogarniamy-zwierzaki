import { useRef, useState, type CSSProperties } from 'react';
import type { Locale, Messages } from '../i18n';
import { countLabel, formatEventDate, interpolate } from '../i18n/format';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api, type SearchResponse, type SearchResult } from '../lib/api';
import { Alert } from './Alert';
import { AnimalAvatar } from './AnimalAvatar';
import { Icon } from './Icon';
import { PageState } from './PageState';

// The API accepts a trimmed query of 1–500 characters.
const maxQueryLength = 500;

// Originals are only ever served by the same-origin API; any other URL is not offered as a link.
const originalUrlPrefix = '/api/documents/';

// The query is kept in component state only: it is never logged or stored in the browser.
type View =
  | { kind: 'idle' }
  | { kind: 'loading'; query: string }
  | { kind: 'done'; query: string; response: SearchResponse }
  | { kind: 'failed'; query: string };

// A multi-page original (a PDF) shows the page within it; a photo or a single-page original shows its position in
// the document, matching the page numbering of the document details page.
function pageText(messages: Messages, result: SearchResult): string {
  return result.filePageCount > 1
    ? interpolate(messages.pageLabel, { position: result.sourcePageNumber, total: result.filePageCount })
    : interpolate(messages.searchPageNumber, { position: result.filePosition + 1 });
}

export function SearchView({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [query, setQuery] = useState('');
  const [view, setView] = useState<View>({ kind: 'idle' });
  const input = useRef<HTMLInputElement>(null);
  // Only the latest search may change the view; an earlier response that arrives late is ignored.
  const latest = useRef(0);

  async function run(text: string) {
    const trimmed = text.trim().slice(0, maxQueryLength);
    if (!trimmed) {
      input.current?.focus();
      return;
    }
    const id = ++latest.current;
    setView({ kind: 'loading', query: trimmed });
    try {
      const response = await api.search(trimmed);
      if (id === latest.current) setView({ kind: 'done', query: trimmed, response });
    } catch (error) {
      if (id !== latest.current) return;
      if (error instanceof ApiError && error.status === 401) {
        window.location.replace(clientLocalePath(locale, 'signin'));
        return;
      }
      // A failure never shows substitute results.
      setView({ kind: 'failed', query: trimmed });
    }
  }

  function runExample(example: string) {
    setQuery(example);
    void run(example);
  }

  function retry(text: string) {
    // The Retry button disappears while the search runs, so focus returns to the search field.
    input.current?.focus();
    void run(text);
  }

  const examples = [messages.searchExample1, messages.searchExample2, messages.searchExample3];
  const response = view.kind === 'done' ? view.response : null;
  const results = response?.items ?? [];

  return <main className="content"><section className="search-page">
    <div className="search-header">
      <h1>{messages.search}</h1>
      <p className="hint" id="search-hint">{messages.searchHint}</p>
    </div>

    <form role="search" className="search-form" onSubmit={(event) => { event.preventDefault(); void run(query); }}>
      <label htmlFor="search-query" className="visually-hidden">{messages.searchLabel}</label>
      <div className="field search-field">
        <Icon name="search" size={22} />
        <input
          ref={input}
          id="search-query"
          type="search"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder={messages.searchPlaceholder}
          maxLength={maxQueryLength}
          autoComplete="off"
          aria-describedby="search-hint"
          enterKeyHint="search"
        />
        <button type="submit" className="primary search-submit">
          <span className="search-submit-label">{messages.search}</span><Icon name="arrowRight" size={18} />
        </button>
      </div>
    </form>

    {view.kind === 'idle' && <div className="search-examples">
      <p className="hint search-examples-label" id="search-examples-label">{messages.searchExamples}</p>
      <div className="chips" role="group" aria-labelledby="search-examples-label">
        {examples.map((example) => <button key={example} type="button" className="chip" onClick={() => runExample(example)}>{example}</button>)}
      </div>
    </div>}

    {view.kind === 'failed' && <Alert
      variant="warning"
      role="alert"
      action={<>
        <button type="button" onClick={() => retry(view.query)}>{messages.retry}</button>
        <a href={clientLocalePath(locale, 'animals')}>{messages.searchGoToAnimals}</a>
      </>}
    >
      <p><strong>{messages.searchUnavailable}</strong></p>
      <p>{messages.searchUnavailableBody}</p>
    </Alert>}

    {/* Persistent live region: progress, the result count and the empty state are announced from here. */}
    <div className="search-status" role="status">
      {view.kind === 'loading' && <div className="search-loading">
        <span>{messages.searching}</span>
        <div className="reading-bar" aria-hidden="true"><span /></div>
      </div>}
      {view.kind === 'done' && results.length > 0 && <p className="search-count">
        <strong>{countLabel(locale, messages, 'documentCount', results.length)}</strong>{' '}
        <span className="muted">{interpolate(messages.searchResultsFor, { query: view.query })}</span>
      </p>}
      {view.kind === 'done' && results.length === 0 && <PageState tone="info" title={messages.searchEmptyTitle}>
        <p>{messages.searchEmptyBody}</p>
      </PageState>}
    </div>

    {response && response.notSearchableCount > 0 && <p className="search-note">
      <Icon name="clock" size={18} />{countLabel(locale, messages, 'notSearchableCount', response.notSearchableCount)}
    </p>}

    {results.length > 0 && <ul className="document-list search-results">
      {results.map((result, index) => {
        const date = formatEventDate(locale, result.eventDate);
        const page = pageText(messages, result);
        const source = `${result.animalName}, ${date}, ${page}`;
        const style = { '--i': Math.min(index, 10) } as CSSProperties;
        return <li key={result.documentId} className="rise" style={style}>
          <article className="card search-result" aria-labelledby={`search-result-${index}`}>
            <header className="search-result-head">
              <AnimalAvatar animal={{ id: result.animalId, name: result.animalName, isActive: true }} locale={locale} size="row" />
              <div className="search-result-meta">
                <h2 id={`search-result-${index}`} className="search-result-animal">{result.animalName}</h2>
                <span className="muted">{`${messages.eventDate}: ${date}`}</span>
              </div>
            </header>
            <blockquote className="search-fragment">{result.fragment}</blockquote>
            <div className="search-result-foot">
              <span className="search-result-page"><Icon name="document" size={16} />{page}</span>
              <div className="original-actions">
                {result.originalUrl.startsWith(originalUrlPrefix) && <a
                  className="secondary"
                  href={result.originalUrl}
                  target="_blank"
                  rel="noopener"
                  aria-label={`${messages.openOriginal}: ${source}`}
                ><Icon name="external" size={16} />{messages.openOriginal}</a>}
                <a
                  className="secondary"
                  href={`${clientLocalePath(locale, 'document')}?id=${encodeURIComponent(result.documentId)}`}
                  aria-label={`${messages.documentDetails}: ${source}`}
                >{messages.documentDetails}<Icon name="chevronRight" size={16} /></a>
              </div>
            </div>
          </article>
        </li>;
      })}
    </ul>}
  </section></main>;
}
