import { useEffect, useState, type ReactNode } from 'react';
import type { Locale, Messages } from '../i18n';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api } from '../lib/api';

type Destination = 'entry' | 'onboarding' | 'protected' | 'signin';

export function SessionGate({ locale, messages, destination, children }: {
  locale: Locale;
  messages: Messages;
  destination: Destination;
  children?: ReactNode;
}) {
  const [status, setStatus] = useState<'loading' | 'ready' | 'failed'>('loading');
  useEffect(() => {
    let active = true;
    void api.me().then((me) => {
      if (!active) return;
      if (destination === 'signin') {
        window.location.replace(clientLocalePath(locale));
      } else if (destination === 'entry') {
        window.location.replace(clientLocalePath(locale, me.hasAnimals ? 'animals' : 'onboarding'));
      } else if (destination === 'onboarding' && me.hasAnimals) {
        window.location.replace(clientLocalePath(locale, 'animals'));
      } else if (destination === 'protected' && !me.hasAnimals) {
        window.location.replace(clientLocalePath(locale, 'onboarding'));
      } else {
        setStatus('ready');
      }
    }).catch((error: unknown) => {
      if (!active) return;
      if (error instanceof ApiError && error.code === 'unauthorized') {
        if (destination === 'signin') {
          setStatus('ready');
        } else {
          window.location.replace(clientLocalePath(locale, 'signin'));
        }
      } else {
        setStatus('failed');
      }
    });
    return () => { active = false; };
  }, [destination, locale]);

  if (status === 'loading') return <main className="content"><p className="loading" role="status">{messages.loading}</p></main>;
  if (status === 'failed') return <main className="content"><p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {messages.request_failed}</p></main>;
  return <>{children}</>;
}
