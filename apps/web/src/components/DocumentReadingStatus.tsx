import { useEffect, useState } from 'react';
import type { Locale, Messages } from '../i18n';
import { formatTimestamp, interpolate } from '../i18n/format';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api, type DocumentReadingStatus as ReadingStatus } from '../lib/api';

// unavailable: the status route answered 404 or failed; the document and its originals stay usable regardless.
type View = 'loading' | 'ready' | 'unavailable' | 'accountChanged';

type MessageKey = keyof Messages;

const pollIntervalMs = 5_000;
const pollLimitMs = 10 * 60 * 1_000;

// Extra detail for the failure codes an owner can act on; other codes only get the generic failure text.
const failureDetails: Record<string, MessageKey> = {
  encrypted_pdf: 'readingFailureEncrypted',
  empty_document: 'readingFailureEmpty',
  page_limit_exceeded: 'readingFailureTooLarge',
  text_limit_exceeded: 'readingFailureTooLarge',
  result_limit_exceeded: 'readingFailureTooLarge',
  unsupported_image_dimensions: 'readingFailureImageSize',
};

function isInProgress(status: ReadingStatus): boolean {
  return status.state === 'queued' || status.state === 'reading';
}

// The reading status of one Stored document, shown next to its originals and fetched independently of the document.
// It polls every five seconds while the content is queued or being read, pauses while the page is hidden, stops when
// unmounted, when the account changes or after ten minutes (then offers Refresh). Retry needs an explicit click and is
// sent once for the account that loaded the page.
export function DocumentReadingStatus({ documentId, locale, messages }: { documentId: string; locale: Locale; messages: Messages }) {
  const [view, setView] = useState<View>('loading');
  const [status, setStatus] = useState<ReadingStatus | null>(null);
  const [accountEmail, setAccountEmail] = useState<string | null>(null);
  // Incremented to (re)start a polling session: on Refresh and after an accepted Retry.
  const [pollSession, setPollSession] = useState(0);
  const [pollingStopped, setPollingStopped] = useState(false);
  const [retrying, setRetrying] = useState(false);
  const [retryError, setRetryError] = useState('');

  // The account the page was loaded for; Retry refuses to act for any other.
  useEffect(() => {
    let active = true;
    void api.me().then((me) => { if (active) setAccountEmail(me.email); }).catch(() => undefined);
    return () => { active = false; };
  }, []);

  const halted = view === 'accountChanged';
  useEffect(() => {
    if (halted) return;
    let cancelled = false;
    let timer: number | undefined;
    let paused = false;
    let inFlight = false;
    const startedAt = Date.now();

    async function poll() {
      timer = undefined;
      if (cancelled || inFlight) return;
      if (document.hidden) {
        paused = true;
        return;
      }
      inFlight = true;
      try {
        const next = await api.documentReading(documentId);
        if (cancelled) return;
        setStatus(next);
        setView('ready');
        if (!isInProgress(next)) return;
        if (Date.now() - startedAt >= pollLimitMs) {
          setPollingStopped(true);
          return;
        }
        timer = window.setTimeout(() => { void poll(); }, pollIntervalMs);
      } catch (error) {
        if (cancelled) return;
        if (error instanceof ApiError && error.code === 'unauthorized') {
          window.location.replace(clientLocalePath(locale, 'signin'));
          return;
        }
        // 404 (the document is gone for this session, for example after an account change) or an outage: say so
        // honestly and stop; the user can refresh.
        setView('unavailable');
      } finally {
        inFlight = false;
      }
    }

    function resume() {
      if (!document.hidden && paused && !cancelled) {
        paused = false;
        void poll();
      }
    }

    setPollingStopped(false);
    document.addEventListener('visibilitychange', resume);
    void poll();
    return () => {
      cancelled = true;
      if (timer !== undefined) window.clearTimeout(timer);
      document.removeEventListener('visibilitychange', resume);
    };
  }, [documentId, locale, pollSession, halted]);

  function refresh() {
    setRetryError('');
    setView((current) => (current === 'ready' ? current : 'loading'));
    setPollSession((session) => session + 1);
  }

  async function retry() {
    if (!accountEmail || retrying) return;
    setRetrying(true);
    setRetryError('');
    try {
      const next = await api.retryDocumentReading(documentId, accountEmail);
      setStatus(next);
      setView('ready');
      if (isInProgress(next)) setPollSession((session) => session + 1);
    } catch (error) {
      const apiError = error instanceof ApiError ? error : null;
      if (apiError?.code === 'account_changed') {
        setView('accountChanged');
      } else if (apiError?.code === 'unauthorized') {
        window.location.replace(clientLocalePath(locale, 'signin'));
      } else if (apiError?.status === 404) {
        setView('unavailable');
      } else if (apiError?.code === 'retry_limited') {
        const retryAfter = apiError.retryAfter;
        setStatus((current) => current && { ...current, canRetry: false, retryAfter: retryAfter ?? current.retryAfter });
        setRetryError(retryAfter ? interpolate(messages.readingRetryLimited, { time: formatTimestamp(locale, retryAfter) }) : messages.readingRetryFailed);
      } else if (apiError?.code === 'invalid_antiforgery_token') {
        setRetryError(messages.readingTokenRejected);
      } else {
        setRetryError(messages.readingRetryFailed);
      }
    } finally {
      setRetrying(false);
    }
  }

  let body;
  if (view === 'loading') {
    body = <p role="status">{messages.readingLoading}</p>;
  } else if (view === 'accountChanged') {
    body = <p role="alert">{messages.readingAccountChanged}</p>;
  } else if (view === 'unavailable' || !status) {
    body = <>
      <p role="status">{messages.readingStatusUnavailable}</p>
      <div className="notice-actions"><button type="button" className="secondary" onClick={refresh}>{messages.readingRefresh}</button></div>
    </>;
  } else {
    const detailKey = status.state === 'failed' && status.failureCode ? failureDetails[status.failureCode] : undefined;
    const showRetryAfter = status.state === 'failed' && !status.canRetry && status.retryAfter && !retryError;
    body = <>
      <p role="status">
        {status.state === 'read' && status.completedAt
          ? interpolate(messages.readingReadOn, { time: formatTimestamp(locale, status.completedAt) })
          : messages[`reading_${status.state}`]}
        {detailKey ? ` ${messages[detailKey]}` : ''}
      </p>
      {showRetryAfter && status.retryAfter ? <p>{interpolate(messages.readingRetryAfter, { time: formatTimestamp(locale, status.retryAfter) })}</p> : null}
      {pollingStopped && isInProgress(status) ? <p>{messages.readingPollingStopped}</p> : null}
      {retryError ? <p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {retryError}</p> : null}
      {status.state === 'failed' || (pollingStopped && isInProgress(status))
        ? <div className="notice-actions">
          {status.state === 'failed'
            ? <button type="button" className="secondary" onClick={() => { void retry(); }} disabled={!status.canRetry || retrying || !accountEmail}>
              {retrying ? messages.readingRetrying : messages.readingRetry}
            </button>
            : null}
          {pollingStopped && isInProgress(status)
            ? <button type="button" className="secondary" onClick={refresh}>{messages.readingRefresh}</button>
            : null}
        </div>
        : null}
    </>;
  }

  return <section className="notice reading-status" aria-labelledby="reading-status-title">
    <h2 id="reading-status-title">{messages.readingTitle}</h2>
    {body}
  </section>;
}
