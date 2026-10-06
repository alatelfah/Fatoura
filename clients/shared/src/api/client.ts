import createFetchClient from 'openapi-fetch';
import createQueryHooks from 'openapi-react-query';
import type { components, paths } from './schema';

export type { components, paths } from './schema';
export type Schemas = components['schemas'];

export interface ApiClientOptions {
  /** Origin of the API, e.g. "" for same-origin web or "https://fatoura.example.com" for mobile. */
  baseUrl: string;
  /** Current access token (kept in memory), or null when signed out. */
  getAccessToken: () => string | null;
  /**
   * Obtains a new access token (web: cookie-based POST /api/auth/refresh; mobile: refresh token from secure storage).
   * Resolve null when the session cannot be renewed.
   */
  refresh: () => Promise<string | null>;
  /** Called once a request failed with 401 and refresh did not help. */
  onSessionExpired?: () => void;
  /** Underlying fetch (defaults to globalThis.fetch). */
  fetch?: typeof fetch;
  credentials?: RequestCredentials;
}

/**
 * fetch wrapper that attaches the bearer token and, on a 401 from a non-auth endpoint, refreshes once
 * (concurrent requests share one refresh) and retries the original request.
 */
export function createAuthFetch(options: ApiClientOptions): typeof fetch {
  const baseFetch = options.fetch ?? globalThis.fetch.bind(globalThis);
  let refreshing: Promise<string | null> | null = null;

  const send = (request: Request, token: string | null) => {
    if (token) request.headers.set('Authorization', `Bearer ${token}`);
    return baseFetch(request);
  };

  return async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, { credentials: options.credentials, ...init });
    const retry = request.clone();
    const response = await send(request, options.getAccessToken());
    if (response.status !== 401 || new URL(request.url).pathname.startsWith('/api/auth/')) {
      return response;
    }

    refreshing ??= options.refresh().finally(() => {
      refreshing = null;
    });
    const token = await refreshing;
    if (!token) {
      options.onSessionExpired?.();
      return response;
    }
    return send(retry, token);
  };
}

export function createApiClient(options: ApiClientOptions) {
  const fetchClient = createFetchClient<paths>({
    baseUrl: options.baseUrl,
    fetch: createAuthFetch(options),
    credentials: options.credentials,
  });
  const $api = createQueryHooks(fetchClient);
  return { fetchClient, $api };
}

export type ApiClient = ReturnType<typeof createApiClient>;

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

/** Normalises an API error body into a message and per-field errors (field names camelCased). */
export function readProblem(error: unknown): { message: string; fields: Record<string, string>; code?: string } {
  const p = (error ?? {}) as ProblemDetails;
  const fields: Record<string, string> = {};
  for (const [key, messages] of Object.entries(p.errors ?? {})) {
    const field = key.replace(/^\$\./, '').replace(/^./, (c) => c.toLowerCase());
    if (messages[0]) fields[field] = messages[0];
  }
  return { message: p.title ?? p.detail ?? 'Request failed.', fields, code: p.code };
}
