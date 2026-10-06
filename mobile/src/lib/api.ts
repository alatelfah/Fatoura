import { Platform } from 'react-native';
import { File, Paths } from 'expo-file-system';
import * as Sharing from 'expo-sharing';
import { createApiClient, type Schemas } from '@fatoura/shared';
import { storage } from './storage';

export type { Schemas };

const REFRESH_KEY = 'fatoura.refreshToken';

/**
 * API origin. Native builds need an absolute URL (EXPO_PUBLIC_API_URL, inlined at build time);
 * the web build defaults to same-origin.
 */
export const API_URL = (process.env.EXPO_PUBLIC_API_URL ?? (Platform.OS === 'web' ? '' : 'http://10.0.2.2:5080')).replace(/\/$/, '');

let accessToken: string | null = null;
let expiredHandler: (() => void) | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function onSessionExpired(handler: (() => void) | null) {
  expiredHandler = handler;
}

export const tokens = {
  getRefresh: () => storage.get(REFRESH_KEY),
  setRefresh: (token: string) => storage.set(REFRESH_KEY, token),
  clear: () => storage.remove(REFRESH_KEY),
};

let refreshing: Promise<Schemas['AuthResponse'] | null> | null = null;

/** Mobile session: refresh token (rotated on every use) in secure storage, access token in memory. Concurrent callers share one request. */
export function refreshSession(): Promise<Schemas['AuthResponse'] | null> {
  refreshing ??= doRefresh().finally(() => {
    refreshing = null;
  });
  return refreshing;
}

async function doRefresh(): Promise<Schemas['AuthResponse'] | null> {
  const refreshToken = await tokens.getRefresh();
  if (!refreshToken) return null;
  const response = await fetch(`${API_URL}/api/auth/refresh`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  }).catch(() => null);
  if (!response?.ok) {
    if (response?.status === 401) await tokens.clear();
    return null;
  }
  const body = (await response.json()) as Schemas['AuthResponse'];
  accessToken = body.accessToken;
  if (body.refreshToken) await tokens.setRefresh(body.refreshToken);
  return body;
}

export async function signIn(email: string, password: string): Promise<{ ok: true; body: Schemas['AuthResponse'] } | { ok: false; status: number }> {
  const response = await fetch(`${API_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ email, password, client: 'Mobile' }),
  });
  if (!response.ok) return { ok: false, status: response.status };
  const body = (await response.json()) as Schemas['AuthResponse'];
  accessToken = body.accessToken;
  if (body.refreshToken) await tokens.setRefresh(body.refreshToken);
  return { ok: true, body };
}

export async function signOut() {
  const refreshToken = await tokens.getRefresh();
  if (refreshToken) {
    await fetch(`${API_URL}/api/auth/logout`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    }).catch(() => undefined);
  }
  accessToken = null;
  await tokens.clear();
}

export const { fetchClient, $api } = createApiClient({
  baseUrl: API_URL,
  getAccessToken: () => accessToken,
  refresh: async () => (await refreshSession())?.accessToken ?? null,
  onSessionExpired: () => expiredHandler?.(),
});

/**
 * Downloads a server-rendered PDF and opens the share sheet (print, email, WhatsApp…).
 * On web the PDF opens in a new tab.
 */
export async function sharePdf(path: string, fileName: string) {
  const url = `${API_URL}${path}`;
  const headers: Record<string, string> = accessToken ? { Authorization: `Bearer ${accessToken}` } : {};
  if (Platform.OS === 'web') {
    const response = await fetch(url, { headers });
    const blob = await response.blob();
    window.open(URL.createObjectURL(blob), '_blank');
    return;
  }
  const target = new File(Paths.cache, fileName);
  if (target.exists) target.delete();
  let file: File;
  try {
    file = await File.downloadFileAsync(url, target, { headers });
  } catch {
    // The access token may have expired: refresh once and retry.
    const renewed = await refreshSession();
    if (!renewed) throw new Error('Session expired');
    file = await File.downloadFileAsync(url, target, { headers: { Authorization: `Bearer ${renewed.accessToken}` } });
  }
  await Sharing.shareAsync(file.uri, { mimeType: 'application/pdf', UTI: 'com.adobe.pdf', dialogTitle: fileName });
}
