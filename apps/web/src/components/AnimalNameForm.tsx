import { useState, type FormEvent, type ReactNode } from 'react';
import type { Messages } from '../i18n';

// Same limit the API applies to the trimmed name.
const nameMaxLength = 100;

// The translated text for an error code, falling back to the generic message for codes without a translation.
export function errorMessage(messages: Messages, code: string): string {
  return Object.hasOwn(messages, code) ? messages[code as keyof Messages] : messages.request_failed;
}

// A name-only animal form. The draft is owned by the caller so it survives failed or stale submissions; submission
// happens only through an explicit submit and is blocked while busy or while the caller reports it as blocked.
export function AnimalNameForm({ messages, id, value, onChange, onSubmit, submitLabel, busy, blocked = false, error, onCancel, autoFocus = false, children }: {
  messages: Messages;
  id?: string;
  value: string;
  onChange: (value: string) => void;
  onSubmit: (name: string) => void;
  submitLabel: string;
  busy: boolean;
  blocked?: boolean;
  error: string;
  onCancel?: () => void;
  autoFocus?: boolean;
  children?: ReactNode;
}) {
  const [validation, setValidation] = useState('');

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || blocked) return;
    const name = value.trim();
    if (!name) { setValidation('name_required'); return; }
    if (name.length > nameMaxLength) { setValidation('name_too_long'); return; }
    setValidation('');
    onSubmit(name);
  }

  const shown = validation || error;
  return <form id={id} className="animal-name-form" onSubmit={submit} noValidate>
    <label>{messages.animalName}<input
      name="name"
      value={value}
      maxLength={nameMaxLength}
      placeholder={messages.animalNamePlaceholder}
      required
      autoComplete="off"
      autoFocus={autoFocus}
      aria-invalid={validation ? true : undefined}
      onChange={(event) => { setValidation(''); onChange(event.currentTarget.value); }}
    /></label>
    {shown && <p className="error" role="alert"><span aria-hidden="true">ⓘ</span> {errorMessage(messages, shown)}</p>}
    {children}
    <div className="form-actions">
      <button className="primary" type="submit" disabled={busy || blocked}>{busy ? messages.saving : submitLabel}</button>
      {onCancel && <button className="secondary" type="button" disabled={busy} onClick={onCancel}>{messages.cancel}</button>}
    </div>
  </form>;
}
