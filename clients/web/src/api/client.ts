import { createApiClient, type Schemas } from '@fatoura/shared';

export type { Schemas };
export type UserInfo = Schemas['UserInfo'];

/**
 * Web session: the access token lives only in memory; the refresh token is an httpOnly cookie scoped to
 * /api/auth, so a page reload restores the session through POST /api/auth/refresh.
 */
let accessToken: string | null = null;
let sessionExpiredHandler: (() => void) | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function onSessionExpired(handler: (() => void) | null) {
  sessionExpiredHandler = handler;
}

export interface AuthResult {
  accessToken: string;
  user: UserInfo;
}

export async function refreshSession(): Promise<AuthResult | null> {
  const response = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' });
  if (!response.ok) return null;
  const body = (await response.json()) as Schemas['AuthResponse'];
  accessToken = body.accessToken;
  return { accessToken: body.accessToken, user: body.user };
}

export const { fetchClient, $api } = createApiClient({
  baseUrl: '',
  credentials: 'same-origin',
  getAccessToken: () => accessToken,
  refresh: async () => (await refreshSession())?.accessToken ?? null,
  onSessionExpired: () => sessionExpiredHandler?.(),
});

/** Fetches a binary endpoint (PDF/Excel) with the bearer token, refreshing once if needed. */
export async function fetchBlob(path: string): Promise<{ blob: Blob; fileName: string | null }> {
  const send = () => fetch(path, { headers: accessToken ? { Authorization: `Bearer ${accessToken}` } : {}, credentials: 'same-origin' });
  let response = await send();
  if (response.status === 401 && (await refreshSession())) response = await send();
  if (!response.ok) throw new Error(`Download failed (${response.status})`);
  const disposition = response.headers.get('content-disposition') ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  return { blob: await response.blob(), fileName: match?.[1] ? decodeURIComponent(match[1]) : null };
}

export async function downloadFile(path: string, fallbackName: string) {
  const { blob, fileName } = await fetchBlob(path);
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName ?? fallbackName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

/** Opens a PDF in a new tab (the browser's viewer handles print and save). */
export async function openPdf(path: string) {
  const tab = window.open('', '_blank');
  const { blob } = await fetchBlob(path);
  const url = URL.createObjectURL(blob);
  if (tab) tab.location.href = url;
  else window.location.href = url;
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/** Uploads a file as multipart/form-data ("file" field). Returns the problem body on failure, null on success. */
export async function uploadFile(path: string, file: File, method: 'PUT' | 'POST' = 'PUT'): Promise<unknown | null> {
  const send = () => {
    const body = new FormData();
    body.append('file', file);
    return fetch(path, { method, body, headers: accessToken ? { Authorization: `Bearer ${accessToken}` } : {}, credentials: 'same-origin' });
  };
  let response = await send();
  if (response.status === 401 && (await refreshSession())) response = await send();
  if (response.ok) return null;
  return response.json().catch(() => ({ title: `Upload failed (${response.status})` }));
}
