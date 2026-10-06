import { describe, expect, it, vi } from 'vitest';
import { createAuthFetch, readProblem } from '../src/api/client';

const json = (status: number, body: unknown = {}) =>
  new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });

describe('createAuthFetch', () => {
  it('attaches the bearer token', async () => {
    const fetchMock = vi.fn(async (_r: Request) => json(200));
    const f = createAuthFetch({ baseUrl: '', getAccessToken: () => 'abc', refresh: async () => null, fetch: fetchMock as never });
    await f('http://x/api/clients');
    expect(fetchMock.mock.calls[0]![0].headers.get('Authorization')).toBe('Bearer abc');
  });

  it('refreshes once on 401 and retries with the new token, sharing one refresh', async () => {
    let token = 'old';
    const fetchMock = vi.fn(async (r: Request) => (r.headers.get('Authorization') === 'Bearer new' ? json(200, { ok: 1 }) : json(401)));
    const refresh = vi.fn(async () => {
      token = 'new';
      return token;
    });
    const f = createAuthFetch({ baseUrl: '', getAccessToken: () => token, refresh, fetch: fetchMock as never });
    const [a, b] = await Promise.all([
      f('http://x/api/clients', { method: 'POST', body: '{"name":"A"}' }),
      f('http://x/api/items'),
    ]);
    expect(a.status).toBe(200);
    expect(b.status).toBe(200);
    expect(refresh).toHaveBeenCalledTimes(1);
    const retried = fetchMock.mock.calls.find((c) => c[0].method === 'POST' && c[0].headers.get('Authorization') === 'Bearer new');
    expect(await retried![0].text()).toBe('{"name":"A"}');
  });

  it('does not refresh for auth endpoints and reports expiry when refresh fails', async () => {
    const onSessionExpired = vi.fn();
    const refresh = vi.fn(async () => null);
    const f = createAuthFetch({ baseUrl: '', getAccessToken: () => null, refresh, onSessionExpired, fetch: (async () => json(401)) as never });
    expect((await f('http://x/api/auth/login', { method: 'POST' })).status).toBe(401);
    expect(refresh).not.toHaveBeenCalled();
    expect((await f('http://x/api/clients')).status).toBe(401);
    expect(onSessionExpired).toHaveBeenCalledTimes(1);
  });
});

describe('readProblem', () => {
  it('maps validation errors to camelCase fields', () => {
    const r = readProblem({ title: 'One or more validation errors occurred.', errors: { Trn: ['TRN must be 15 digits.'], '$.email': ['bad'] } });
    expect(r.fields).toEqual({ trn: 'TRN must be 15 digits.', email: 'bad' });
  });
});
