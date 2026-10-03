export interface MeResponse {
  email: string;
  hasAnimals: boolean;
}

export interface Animal {
  id: string;
  name: string;
}

export class ApiError extends Error {
  readonly code: string;

  constructor(code: string) {
    super(code);
    this.name = 'ApiError';
    this.code = code;
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: { ...(init.body ? { 'Content-Type': 'application/json' } : {}), ...init.headers },
    credentials: 'same-origin',
  });
  if (!response.ok) {
    let code = response.status === 401 ? 'unauthorized' : 'request_failed';
    try {
      const problem: unknown = await response.json();
      if (typeof problem === 'object' && problem !== null && 'code' in problem && typeof problem.code === 'string') {
        code = problem.code;
      }
    } catch {
      // The response may not contain JSON ProblemDetails.
    }
    throw new ApiError(code);
  }
  if (response.status === 204 || response.status === 201 && !response.headers.get('content-type')) {
    return undefined as T;
  }
  return response.json() as Promise<T>;
}

export const api = {
  me: () => request<MeResponse>('/api/me'),
  register: (email: string, password: string) => request<void>('/api/auth/register', { method: 'POST', body: JSON.stringify({ email, password }) }),
  login: (email: string, password: string) => request<void>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
  logout: () => request<void>('/api/auth/logout', { method: 'POST', body: '{}' }),
  animals: () => request<Animal[]>('/api/animals/'),
  createAnimal: (name: string) => request<Animal>('/api/animals/', { method: 'POST', body: JSON.stringify({ name }) }),
};
