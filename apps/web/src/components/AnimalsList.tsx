import { useEffect, useState } from 'react';
import type { Locale, Messages } from '../i18n';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api, type Animal } from '../lib/api';

export function AnimalsList({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [animals, setAnimals] = useState<Animal[] | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => { void api.animals().then(setAnimals).catch((error: unknown) => {
    if (error instanceof ApiError && error.code === 'unauthorized') {
      window.location.replace(clientLocalePath(locale, 'signin'));
      return;
    }
    setFailed(true);
  }); }, [locale]);
  return <main className="content"><section className="animal-section">
    <h1>{messages.animalsTitle}</h1>
    {failed && <p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {messages.request_failed}</p>}
    {animals === null && !failed ? <p className="loading" role="status">{messages.loading}</p> : null}
    {animals?.length === 0 && <p>{messages.noAnimals}</p>}
    {animals && animals.length > 0 && <ul className="animal-list">{animals.map((animal) => <li key={animal.id}><span className="paw" aria-hidden="true">♧</span><span>{animal.name}</span></li>)}</ul>}
  </section></main>;
}
