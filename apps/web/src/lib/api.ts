export interface MeResponse {
  email: string;
  hasAnimals: boolean;
}

export interface Animal {
  id: string;
  name: string;
}

// The animals a capture can be assigned to and the preselected one (the account's last completed capture's animal
// while it is still eligible). defaultAnimalId is null only when the account has no animals.
export interface CaptureDefaults {
  animals: Animal[];
  defaultAnimalId: string | null;
}

// One original of a capture manifest, in its confirmed position. sha256 is the lowercase hex digest.
export interface UploadFileManifest {
  name: string;
  contentType: string;
  byteLength: number;
  sha256: string;
}

// The immutable capture manifest. eventDate is yyyy-MM-dd; timeZone the browser's IANA time zone.
export interface UploadManifest {
  animalId: string;
  eventDate: string;
  timeZone: string;
  files: UploadFileManifest[];
}

export interface UploadFileSlot {
  position: number;
  fileId: string;
  name: string;
  contentType: string;
  byteLength: number;
  sha256: string;
  stored: boolean;
}

export interface DocumentUpload {
  operationId: string;
  animalId: string;
  eventDate: string;
  timeZone: string;
  state: 'uploading' | 'stored';
  files: UploadFileSlot[];
}

// originalUrl is the authenticated same-origin API route; appending ?download=true asks for an attachment.
export interface StoredDocumentFile {
  id: string;
  position: number;
  originalName: string;
  contentType: string;
  byteLength: number;
  originalUrl: string;
}

export interface StoredDocument {
  id: string;
  animalId: string;
  animalName: string;
  eventDate: string;
  uploadedAt: string;
  files: StoredDocumentFile[];
}

export interface DocumentSummary {
  id: string;
  animalId: string;
  eventDate: string;
  uploadedAt: string;
  fileCount: number;
}

export interface DocumentPage {
  items: DocumentSummary[];
  hasMore: boolean;
}

interface AntiforgeryToken {
  token: string;
  headerName: string;
}

// code is the ProblemDetails `code` when the response carried one, otherwise a fallback derived from the HTTP
// status (proxy-generated errors may have no JSON body). status is 0 when no HTTP response was received.
export class ApiError extends Error {
  readonly code: string;
  readonly status: number;

  constructor(code: string, status = 0) {
    super(code);
    this.name = 'ApiError';
    this.code = code;
    this.status = status;
  }
}

interface RequestOptions {
  method?: string;
  // Sent as JSON with an explicit Content-Type.
  json?: unknown;
  // Sent as multipart/form-data; the browser sets the Content-Type with its own boundary.
  form?: FormData;
  headers?: Record<string, string>;
}

function fallbackCode(status: number): string {
  switch (status) {
    case 401: return 'unauthorized';
    case 404: return 'not_found';
    case 413: return 'file_too_large';
    default: return 'request_failed';
  }
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { ...options.headers };
  let body: BodyInit | undefined;
  if (options.form) {
    body = options.form;
  } else if (options.json !== undefined) {
    headers['Content-Type'] = 'application/json';
    body = JSON.stringify(options.json);
  }
  const response = await fetch(path, { method: options.method ?? 'GET', headers, body, credentials: 'same-origin' });
  if (!response.ok) {
    let code = fallbackCode(response.status);
    try {
      const problem: unknown = await response.json();
      if (typeof problem === 'object' && problem !== null && 'code' in problem && typeof problem.code === 'string') {
        code = problem.code;
      }
    } catch {
      // The response may not contain JSON ProblemDetails (for example a proxy error page).
    }
    if (response.status === 401) antiforgery = null;
    throw new ApiError(code, response.status);
  }
  if (response.status === 204 || response.status === 201 && !response.headers.get('content-type')) {
    return undefined as T;
  }
  return response.json() as Promise<T>;
}

// The antiforgery request token is tied to the signed-in account, so it is cached only for this page and dropped
// whenever the API reports a token or session problem.
let antiforgery: Promise<AntiforgeryToken> | null = null;

function antiforgeryToken(): Promise<AntiforgeryToken> {
  antiforgery ??= request<AntiforgeryToken>('/api/antiforgery').catch((error: unknown) => {
    antiforgery = null;
    throw error;
  });
  return antiforgery;
}

// Sends a capture mutation with the antiforgery header. Capture mutations are idempotent by operation ID, so after a
// rejected token the call is repeated once with a fresh token — but only when the session still belongs to the
// account that started the capture; otherwise it fails with `account_changed` so no pending file can reach another
// account. Legacy (non-idempotent) calls never go through this path and are never replayed.
async function captureMutation<T>(path: string, options: RequestOptions, accountEmail: string): Promise<T> {
  for (let attempt = 0; ; attempt++) {
    const token = await antiforgeryToken();
    try {
      return await request<T>(path, { ...options, headers: { ...options.headers, [token.headerName]: token.token } });
    } catch (error) {
      if (!(error instanceof ApiError) || error.code !== 'invalid_antiforgery_token' || attempt > 0) throw error;
      antiforgery = null;
      const me = await request<MeResponse>('/api/me');
      if (me.email !== accountEmail) throw new ApiError('account_changed', 401);
    }
  }
}

function uploadPath(operationId: string): string {
  return `/api/document-uploads/${encodeURIComponent(operationId)}`;
}

export const api = {
  me: () => request<MeResponse>('/api/me'),
  register: (email: string, password: string) => request<void>('/api/auth/register', { method: 'POST', json: { email, password } }),
  login: (email: string, password: string) => request<void>('/api/auth/login', { method: 'POST', json: { email, password } }),
  logout: () => request<void>('/api/auth/logout', { method: 'POST', json: {} }),
  animals: () => request<Animal[]>('/api/animals/'),
  animal: (id: string) => request<Animal>(`/api/animals/${encodeURIComponent(id)}`),
  createAnimal: (name: string) => request<Animal>('/api/animals/', { method: 'POST', json: { name } }),
  captureDefaults: () => request<CaptureDefaults>('/api/capture-defaults'),
  createUpload: (operationId: string, manifest: UploadManifest, accountEmail: string) =>
    captureMutation<DocumentUpload>(`${uploadPath(operationId)}/`, { method: 'PUT', json: manifest }, accountEmail),
  uploadFile: (operationId: string, position: number, file: File, accountEmail: string) => {
    const form = new FormData();
    form.append('file', file, file.name);
    return captureMutation<UploadFileSlot>(`${uploadPath(operationId)}/files/${position}`, { method: 'PUT', form }, accountEmail);
  },
  completeUpload: (operationId: string, accountEmail: string) =>
    captureMutation<StoredDocument>(`${uploadPath(operationId)}/complete`, { method: 'POST' }, accountEmail),
  animalDocuments: (animalId: string, offset: number, limit: number) =>
    request<DocumentPage>(`/api/animals/${encodeURIComponent(animalId)}/documents?offset=${offset}&limit=${limit}`),
  document: (id: string) => request<StoredDocument>(`/api/documents/${encodeURIComponent(id)}`),
};
