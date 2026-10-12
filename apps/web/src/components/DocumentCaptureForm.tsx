import { useEffect, useId, useRef, useState, type ChangeEvent } from 'react';
import type { Locale, Messages } from '../i18n';
import { countLabel, formatEventDate, interpolate, localToday, uuidQueryParameter } from '../i18n/format';
import { clientLocalePath } from '../i18n/paths';
import { ApiError, api, type Animal, type UploadManifest } from '../lib/api';
import { Alert, FieldError } from './Alert';
import { Icon } from './Icon';
import { PageState } from './PageState';

const maxFileBytes = 10_485_760;
const maxPhotos = 10;

type SupportedType = 'application/pdf' | 'image/jpeg' | 'image/png';
type Kind = 'pdf' | 'image';

interface SelectedFile {
  key: string;
  file: File;
  contentType: SupportedType;
  sha256: string;
}

interface Selection {
  kind: Kind;
  files: SelectedFile[];
}

// A capture operation frozen at the first Save: retrying reuses the same operation ID, manifest and files.
interface Attempt {
  operationId: string;
  manifest: UploadManifest;
  files: File[];
  accountEmail: string;
}

type Progress =
  | { phase: 'hashing' }
  | { phase: 'creating' }
  | { phase: 'uploading'; current: number; total: number }
  | { phase: 'completing' }
  | { phase: 'done' };

type Status = 'editing' | 'running' | 'failed' | 'sessionExpired';

// Failures that the same operation can recover from by trying again; anything else needs a changed selection.
const retryableCodes = new Set(['upload_interrupted', 'storage_unavailable', 'upload_incomplete']);

class SelectionError extends Error {}

// Where an error is shown: next to the zone it concerns, or above Save/Retry for everything else. Presentation only;
// the form still keeps a single error code.
type ErrorZone = 'files' | 'animal' | 'date' | 'form';
const filesZoneCodes = new Set(['files_required', 'empty_file', 'file_too_large', 'heic_not_supported', 'unsupported_file_type', 'one_pdf_only', 'too_many_photos', 'duplicate_photo', 'file_unreadable', 'insecure_context']);
const animalZoneCodes = new Set(['animal_required', 'animal_inactive']);
const dateZoneCodes = new Set(['invalid_event_date', 'future_event_date']);
// The selection is still valid after these, so they are warnings rather than errors.
const warningCodes = new Set(['duplicate_photo', 'too_many_photos']);

function errorZone(code: string): ErrorZone {
  if (filesZoneCodes.has(code)) return 'files';
  if (animalZoneCodes.has(code)) return 'animal';
  if (dateZoneCodes.has(code)) return 'date';
  return 'form';
}

// Recognises the format from the file's leading bytes (cameras may report no or a wrong MIME type).
async function detectType(file: File): Promise<SupportedType | 'heic' | null> {
  const head = new Uint8Array(await file.slice(0, 16).arrayBuffer());
  const startsWith = (signature: number[], offset = 0) => signature.every((byte, index) => head[offset + index] === byte);
  if (startsWith([0x25, 0x50, 0x44, 0x46, 0x2d])) return 'application/pdf';
  if (startsWith([0xff, 0xd8, 0xff])) return 'image/jpeg';
  if (startsWith([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])) return 'image/png';
  const brand = String.fromCharCode(...head.slice(8, 12));
  const isoMedia = startsWith([0x66, 0x74, 0x79, 0x70], 4);
  if (isoMedia && ['heic', 'heix', 'hevc', 'hevx', 'heim', 'heis', 'mif1', 'msf1'].includes(brand)) return 'heic';
  if (/^image\/hei[cf]/i.test(file.type) || /\.hei[cf]$/i.test(file.name)) return 'heic';
  return null;
}

// Web Crypto (hashing and operation IDs) exists only on HTTPS or localhost.
function secureContext(): boolean {
  return window.isSecureContext && typeof crypto !== 'undefined' && typeof crypto.subtle !== 'undefined';
}

async function sha256Hex(file: File): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', await file.arrayBuffer());
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('');
}

async function checkFiles(files: File[]): Promise<SelectedFile[]> {
  const checked: SelectedFile[] = [];
  for (const file of files) {
    if (file.size === 0) throw new SelectionError('empty_file');
    if (file.size > maxFileBytes) throw new SelectionError('file_too_large');
    let type: Awaited<ReturnType<typeof detectType>>;
    try {
      type = await detectType(file);
    } catch {
      throw new SelectionError('file_unreadable');
    }
    if (type === 'heic') throw new SelectionError('heic_not_supported');
    if (type === null) throw new SelectionError('unsupported_file_type');
    let sha256: string;
    try {
      sha256 = await sha256Hex(file);
    } catch {
      throw new SelectionError('file_unreadable');
    }
    checked.push({ key: crypto.randomUUID(), file, contentType: type, sha256 });
  }
  const pdfCount = checked.filter((item) => item.contentType === 'application/pdf').length;
  if (pdfCount > 0 && checked.length > 1) throw new SelectionError('one_pdf_only');
  return checked;
}

export function DocumentCaptureForm({ locale, messages }: { locale: Locale; messages: Messages }) {
  const [loadState, setLoadState] = useState<'loading' | 'ready' | 'failed'>('loading');
  const [accountEmail, setAccountEmail] = useState('');
  const [animals, setAnimals] = useState<Animal[]>([]);
  const [animalId, setAnimalId] = useState('');
  const [eventDate, setEventDate] = useState(localToday);
  const [selection, setSelection] = useState<Selection | null>(null);
  const [replacement, setReplacement] = useState<Selection | null>(null);
  const [previews, setPreviews] = useState<Record<string, string>>({});
  const [status, setStatus] = useState<Status>('editing');
  const [progress, setProgress] = useState<Progress | null>(null);
  const [errorCode, setErrorCode] = useState('');
  const [attempt, setAttempt] = useState<Attempt | null>(null);
  const completedRef = useRef(false);
  const selectionRef = useRef(selection);
  selectionRef.current = selection;
  const cameraInput = useRef<HTMLInputElement>(null);
  const photosInput = useRef<HTMLInputElement>(null);
  const pdfInput = useRef<HTMLInputElement>(null);
  const filesErrorId = useId();
  const animalErrorId = useId();
  const dateErrorId = useId();

  useEffect(() => {
    let active = true;
    void Promise.all([api.me(), api.captureDefaults()]).then(([me, defaults]) => {
      if (!active) return;
      setAccountEmail(me.email);
      setAnimals(defaults.animals);
      // An animal chosen on its document list wins over the account default when it is still eligible.
      const requested = uuidQueryParameter('animalId')?.toLowerCase();
      const preselected = defaults.animals.find((animal) => animal.id.toLowerCase() === requested)?.id;
      setAnimalId(preselected ?? defaults.defaultAnimalId ?? defaults.animals[0]?.id ?? '');
      setLoadState('ready');
    }).catch((error: unknown) => {
      if (!active) return;
      if (error instanceof ApiError && error.code === 'unauthorized') {
        window.location.replace(clientLocalePath(locale, 'signin'));
        return;
      }
      setLoadState('failed');
    });
    return () => { active = false; };
  }, [locale]);

  // Previews exist only for the current image selection; they are revoked when it changes or the form unmounts.
  // The selection, manifest and operation ID live in state, so revoking previews never discards a pending retry.
  useEffect(() => {
    const urls: Record<string, string> = {};
    if (selection?.kind === 'image') {
      for (const item of selection.files) urls[item.key] = URL.createObjectURL(item.file);
    }
    setPreviews(urls);
    return () => { for (const url of Object.values(urls)) URL.revokeObjectURL(url); };
  }, [selection]);

  // Leaving mid-upload abandons the operation, so the browser asks first; the redirect after completion is exempt.
  useEffect(() => {
    if (status !== 'running') return;
    const warn = (event: BeforeUnloadEvent) => {
      if (completedRef.current) return;
      event.preventDefault();
    };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [status]);

  const frozen = status !== 'editing';
  const today = localToday();
  const selectedAnimal = animals.find((animal) => animal.id === animalId);

  async function addFiles(event: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.currentTarget.files ?? []);
    // Allows choosing the same file again later.
    event.currentTarget.value = '';
    if (files.length === 0) return;
    setErrorCode('');
    setReplacement(null);
    if (!secureContext()) {
      setErrorCode('insecure_context');
      return;
    }
    let checked: SelectedFile[];
    try {
      checked = await checkFiles(files);
    } catch (error) {
      setErrorCode(error instanceof SelectionError ? error.message : 'file_unreadable');
      return;
    }
    const kind: Kind = checked[0]?.contentType === 'application/pdf' ? 'pdf' : 'image';
    const current = selectionRef.current;
    if (kind === 'image' && current?.kind === 'image') {
      // The same photo (same content, whatever its name) is added only once per document.
      const known = new Set(current.files.map((item) => item.sha256));
      const fresh = checked.filter((item) => !known.has(item.sha256) && known.add(item.sha256));
      if (fresh.length < checked.length) setErrorCode('duplicate_photo');
      if (fresh.length === 0) return;
      if (current.files.length + fresh.length > maxPhotos) {
        setErrorCode('too_many_photos');
        return;
      }
      setSelection({ kind, files: [...current.files, ...fresh] });
      return;
    }
    if (kind === 'image') {
      const unique = checked.filter((item, index) => checked.findIndex((other) => other.sha256 === item.sha256) === index);
      if (unique.length < checked.length) setErrorCode('duplicate_photo');
      checked = unique;
    }
    if (kind === 'image' && checked.length > maxPhotos) {
      setErrorCode('too_many_photos');
      return;
    }
    // A PDF excludes photos (and another PDF): replacing an existing selection needs an explicit choice.
    if (current && current.files.length > 0) {
      setReplacement({ kind, files: checked });
      return;
    }
    setSelection({ kind, files: checked });
  }

  function confirmReplacement() {
    if (!replacement) return;
    setSelection(replacement);
    setReplacement(null);
    setErrorCode('');
  }

  function move(index: number, offset: -1 | 1) {
    if (!selection) return;
    const target = index + offset;
    if (target < 0 || target >= selection.files.length) return;
    const files = [...selection.files];
    const [item] = files.splice(index, 1);
    if (!item) return;
    files.splice(target, 0, item);
    setSelection({ ...selection, files });
  }

  function remove(key: string) {
    if (!selection) return;
    const files = selection.files.filter((item) => item.key !== key);
    setSelection(files.length > 0 ? { ...selection, files } : null);
    setErrorCode('');
  }

  function fail(code: string) {
    setErrorCode(code);
    return false;
  }

  function validate(): boolean {
    if (!selection || selection.files.length === 0) return fail('files_required');
    if (!animalId) return fail('animal_required');
    if (!/^\d{4}-\d{2}-\d{2}$/.test(eventDate)) return fail('invalid_event_date');
    if (eventDate > localToday()) return fail('future_event_date');
    return true;
  }

  async function save() {
    if (frozen) return;
    setErrorCode('');
    setReplacement(null);
    if (!validate() || !selection) return;
    if (!secureContext()) {
      setErrorCode('insecure_context');
      return;
    }
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    if (!timeZone) {
      setErrorCode('invalid_time_zone');
      return;
    }
    setStatus('running');
    setProgress({ phase: 'hashing' });
    const manifestFiles: UploadManifest['files'] = [];
    try {
      for (const item of selection.files) {
        manifestFiles.push({ name: item.file.name, contentType: item.contentType, byteLength: item.file.size, sha256: item.sha256 });
      }
    } catch {
      setStatus('editing');
      setProgress(null);
      setErrorCode('file_unreadable');
      return;
    }
    const frozenAttempt: Attempt = {
      operationId: crypto.randomUUID(),
      manifest: { animalId, eventDate, timeZone, files: manifestFiles },
      files: selection.files.map((item) => item.file),
      accountEmail,
    };
    setAttempt(frozenAttempt);
    await run(frozenAttempt);
  }

  async function run(current: Attempt) {
    setStatus('running');
    setErrorCode('');
    let stage: 'create' | 'upload' | 'complete' = 'create';
    try {
      setProgress({ phase: 'creating' });
      const upload = await api.createUpload(current.operationId, current.manifest, current.accountEmail);
      stage = 'upload';
      const total = current.files.length;
      const pending = [...upload.files].sort((a, b) => a.position - b.position).filter((slot) => !slot.stored);
      for (const slot of pending) {
        const file = current.files[slot.position];
        if (!file) throw new ApiError('upload_conflict');
        setProgress({ phase: 'uploading', current: slot.position + 1, total });
        await api.uploadFile(current.operationId, slot.position, file, current.accountEmail);
      }
      stage = 'complete';
      setProgress({ phase: 'completing' });
      const document = await api.completeUpload(current.operationId, current.accountEmail);
      setProgress({ phase: 'done' });
      completedRef.current = true;
      window.location.assign(`${clientLocalePath(locale, 'document')}?id=${encodeURIComponent(document.id)}`);
    } catch (error) {
      const apiError = error instanceof ApiError ? error : null;
      if (apiError && (apiError.status === 401 || apiError.code === 'account_changed')) {
        // Pending files are never carried over to a new session or another account.
        setSelection(null);
        setReplacement(null);
        setAttempt(null);
        setProgress(null);
        setStatus('sessionExpired');
        return;
      }
      if (stage === 'create' && apiError?.code === 'animal_inactive') {
        // Only a manifest that was never accepted can hit this (an accepted one is retried whatever the animal's state).
        await recoverFromInactiveAnimal(current.manifest.animalId);
        return;
      }
      let code = apiError?.code ?? 'upload_interrupted';
      if (code === 'not_found') code = stage === 'create' ? 'animal_unavailable' : 'upload_not_found';
      if (code === 'unsupported_media_type') code = 'unsupported_file_type';
      if (code === 'request_failed' || code === 'invalid_antiforgery_token' || !Object.hasOwn(messages, code)) {
        // Only network failures, server errors and a rejected token can succeed on retry; other 4xx need a new selection.
        const transient = !apiError || apiError.status === 0 || apiError.status >= 500 || code === 'invalid_antiforgery_token';
        code = transient ? 'upload_interrupted' : 'upload_rejected';
      }
      setStatus('failed');
      setErrorCode(code);
    }
  }

  // The animal became inactive before its manifest was accepted. The files and date stay; the operation is dropped
  // (the next Save gets a new operation ID) and nothing is reassigned until the user picks an eligible animal.
  async function recoverFromInactiveAnimal(inactiveId: string) {
    setAttempt(null);
    setProgress(null);
    setAnimalId('');
    setStatus('editing');
    setErrorCode('animal_inactive');
    try {
      const defaults = await api.captureDefaults();
      setAnimals(defaults.animals);
    } catch {
      setAnimals((current) => current.filter((animal) => animal.id !== inactiveId));
    }
  }

  function startOver() {
    setAttempt(null);
    setProgress(null);
    setErrorCode('');
    setStatus('editing');
  }

  function progressText(current: Progress): string {
    switch (current.phase) {
      case 'uploading': return interpolate(messages.phase_uploading, { current: current.current, total: current.total });
      default: return messages[`phase_${current.phase}`];
    }
  }

  function progressValue(current: Progress, total: number): number | undefined {
    switch (current.phase) {
      case 'hashing': return undefined;
      case 'creating': return 0;
      case 'uploading': return current.current;
      case 'completing': return total + 1;
      case 'done': return total + 2;
    }
  }

  const message = (code: string) => (Object.hasOwn(messages, code) ? messages[code as keyof Messages] : messages.request_failed);

  if (loadState === 'loading') return <main className="content"><p className="loading" role="status">{messages.loadingContent}</p></main>;
  if (loadState === 'failed') return <main className="content"><PageState tone="error" headingLevel={1} title={messages.request_failed} /></main>;

  if (status === 'sessionExpired') {
    return <main className="content"><section className="card capture-card capture-state">
      <h1>{messages.captureTitle}</h1>
      <Alert variant="error">{messages.session_expired}</Alert>
      <a className="primary button-link" href={clientLocalePath(locale, 'signin')}>{messages.signInAgain}</a>
    </section></main>;
  }

  // Inactive animals take no fresh capture. This is not onboarding: the account may still own inactive animals.
  if (animals.length === 0 && !attempt) {
    return <main className="content"><section className="card capture-card capture-state">
      <h1>{messages.captureTitle}</h1>
      <Alert variant="info">{messages.noActiveAnimals}</Alert>
      <a className="primary button-link" href={clientLocalePath(locale, 'animals')}>{messages.noActiveAnimalsAction}</a>
    </section></main>;
  }

  const fileTotal = attempt?.files.length ?? selection?.files.length ?? 0;
  const zone = errorCode ? errorZone(errorCode) : null;
  // Every capture error was announced at once before the redesign; zone alerts keep role="alert", warnings included.
  const zoneAlert = (id?: string) => <Alert variant={warningCodes.has(errorCode) ? 'warning' : 'error'} role="alert" id={id}>{message(errorCode)}</Alert>;
  const showProgress = progress !== null && status === 'running';
  return <main className="content"><section className="capture-card">
    <header className="capture-header">
      <h1>{messages.captureTitle}</h1>
      <p className="muted">{messages.captureIntro}</p>
    </header>
    <form onSubmit={(event) => { event.preventDefault(); void save(); }} noValidate>
      <fieldset className="capture-fields" disabled={frozen}>
        <div className="capture-sources">
          <button type="button" className="source-button source-camera" onClick={() => cameraInput.current?.click()}><span className="source-icon" aria-hidden="true"><Icon name="camera" size={22} /></span>{messages.takePhoto}</button>
          <button type="button" className="source-button source-photos" onClick={() => photosInput.current?.click()}><span className="source-icon" aria-hidden="true"><Icon name="photos" size={22} /></span>{messages.addPhotos}</button>
          <button type="button" className="source-button source-pdf" onClick={() => pdfInput.current?.click()}><span className="source-icon" aria-hidden="true"><Icon name="pdf" size={22} /></span>{messages.choosePdf}</button>
          <input ref={cameraInput} type="file" accept="image/jpeg,image/png" capture="environment" hidden onChange={(event) => void addFiles(event)} />
          <input ref={photosInput} type="file" accept="image/jpeg,image/png" multiple hidden onChange={(event) => void addFiles(event)} />
          <input ref={pdfInput} type="file" accept="application/pdf" hidden onChange={(event) => void addFiles(event)} />
        </div>
        <p className="hint capture-limits"><Icon name="info" size={18} />{messages.captureLimits}</p>

        {zone === 'files' && zoneAlert(filesErrorId)}

        {replacement && <div className="replace-notice" role="group" aria-label={messages.selectedFiles}>
          <Alert
            variant="warning"
            role={null}
            action={<>
              <button type="button" onClick={confirmReplacement}>{messages.replaceSelection}</button>
              <button type="button" className="alert-action-quiet" onClick={() => setReplacement(null)}>{messages.keepSelection}</button>
            </>}
          >{replacement.kind === 'pdf' ? messages.replaceWithPdfPrompt : messages.replaceWithPhotosPrompt}</Alert>
        </div>}

        <section className="card selection" aria-label={messages.selectedFiles} aria-describedby={zone === 'files' ? filesErrorId : undefined}>
          <h2>{messages.selectedFiles}{selection?.kind === 'image' ? <span className="count"> · {countLabel(locale, messages, 'photoCount', selection.files.length)}</span> : null}</h2>
          {!selection && <p className="hint">{messages.selectionEmpty}</p>}
          {selection && <ol className="file-list">{selection.files.map((item, index) => <li key={item.key} className="file-item">
            {selection.kind === 'image'
              ? previews[item.key] ? <img className="file-preview" src={previews[item.key]} alt={interpolate(messages.photoPreviewAlt, { position: index + 1 })} /> : <span className="file-preview" aria-hidden="true" />
              : <span className="file-preview file-pdf" aria-hidden="true"><Icon name="pdf" size={26} /></span>}
            <span className="file-name">{item.file.name}</span>
            <span className="file-actions">
              {selection.kind === 'image' && <>
                <button type="button" className="icon-button" disabled={index === 0} onClick={() => move(index, -1)} aria-label={interpolate(messages.moveUpLabel, { position: index + 1 })}><Icon name="arrowUp" size={18} /></button>
                <button type="button" className="icon-button" disabled={index === selection.files.length - 1} onClick={() => move(index, 1)} aria-label={interpolate(messages.moveDownLabel, { position: index + 1 })}><Icon name="arrowDown" size={18} /></button>
              </>}
              <button type="button" className="icon-button icon-button-danger" onClick={() => remove(item.key)} aria-label={interpolate(messages.removeLabel, { name: item.file.name })}><Icon name="close" size={18} /></button>
            </span>
          </li>)}</ol>}
        </section>

        <fieldset className="chip-group" aria-describedby={zone === 'animal' ? animalErrorId : undefined}>
          <legend>{messages.animal}</legend>
          <div className="chips">{animals.map((animal) => <label key={animal.id} className={animal.id === animalId ? 'chip selected' : 'chip'}>
            <input type="radio" name="animal" value={animal.id} checked={animal.id === animalId} onChange={() => setAnimalId(animal.id)} className="visually-hidden" />
            <Icon name="check" size={18} />{animal.name}
          </label>)}</div>
          {zone === 'animal' && zoneAlert(animalErrorId)}
        </fieldset>

        <div className="field-group date-field">
          <label>{messages.eventDate}<span className={zone === 'date' ? 'field invalid' : 'field'}><Icon name="calendar" size={22} /><input
            type="date"
            name="eventDate"
            value={eventDate}
            max={today}
            required
            aria-invalid={zone === 'date' ? true : undefined}
            aria-describedby={zone === 'date' ? dateErrorId : undefined}
            onChange={(event) => setEventDate(event.currentTarget.value)}
          /></span></label>
          <div className="field-error-slot" role="alert">{zone === 'date' && <FieldError id={dateErrorId}>{message(errorCode)}</FieldError>}</div>
        </div>
      </fieldset>

      {selectedAnimal && /^\d{4}-\d{2}-\d{2}$/.test(eventDate) && <p className="capture-summary">{interpolate(messages.captureSummary, { animal: selectedAnimal.name, date: formatEventDate(locale, eventDate) })}</p>}

      {/* The phase paragraph is a persistent live region, so the first phase is announced too; the card styling and
          the bar appear only while saving. */}
      <div className={showProgress ? 'card capture-progress' : 'capture-progress idle'}>
        <div className="capture-phase">
          {showProgress && progress.phase === 'done' && <span className="tile capture-done-tile" aria-hidden="true"><Icon name="check" size={24} className="draw" /></span>}
          <p role="status">{showProgress ? progressText(progress) : ''}</p>
        </div>
        {showProgress && <progress className="capture-bar" aria-label={messages.progressLabel} max={fileTotal + 2} value={progressValue(progress, fileTotal)} />}
      </div>

      {zone === 'form' && zoneAlert()}

      {status === 'failed' && attempt
        ? <div className="capture-actions">
          {retryableCodes.has(errorCode) && <button type="button" className="primary" onClick={() => void run(attempt)}>{messages.retry}</button>}
          {!retryableCodes.has(errorCode) && <button type="button" className="secondary" onClick={startOver}>{messages.startOver}</button>}
        </div>
        : <button className="primary capture-save" type="submit" disabled={frozen}>{status === 'running' ? messages.saving : messages.save}</button>}
    </form>
  </section></main>;
}
