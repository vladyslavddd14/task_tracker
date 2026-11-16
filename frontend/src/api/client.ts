const TOKEN_KEY = 'tt.token';

/** Error returned by the API (RFC 7807 problem details). */
export class ApiError extends Error {
  readonly status: number;
  readonly errors?: Record<string, string[]>;

  constructor(status: number, message: string, errors?: Record<string, string[]>) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.errors = errors;
  }
}

export const tokenStore = {
  get(): string | null {
    try {
      return window.localStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  },
  set(token: string): void {
    try {
      window.localStorage.setItem(TOKEN_KEY, token);
    } catch {
      // Storage can be unavailable (private mode); the session then lasts until reload.
    }
  },
  clear(): void {
    try {
      window.localStorage.removeItem(TOKEN_KEY);
    } catch {
      // Nothing to clear.
    }
  },
};

let unauthorizedHandler: (() => void) | null = null;

/** Called when a protected request returns 401 (the app then redirects to the login page). */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

type QueryValue = string | number | boolean | null | undefined | (string | number)[];

/** Builds a query string. Arrays become repeated parameters; empty values are skipped. */
export function toQuery(params: Record<string, QueryValue>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (Array.isArray(value)) {
      value.forEach((item) => search.append(key, String(item)));
    } else if (value !== undefined && value !== null && value !== '') {
      search.append(key, String(value));
    }
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}

async function readProblem(response: Response): Promise<ApiError> {
  let message = response.statusText;
  let errors: Record<string, string[]> | undefined;
  try {
    const body = await response.json();
    message = body.detail ?? body.title ?? message;
    errors = body.errors;
  } catch {
    // The body is empty or not JSON.
  }
  return new ApiError(response.status, message, errors);
}

export async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  const token = tokenStore.get();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(`/api${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    // A 401 on the login/register calls just means wrong credentials, not an expired session.
    if (response.status === 401 && token && !path.startsWith('/auth/')) {
      tokenStore.clear();
      unauthorizedHandler?.();
    }
    throw await readProblem(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}
