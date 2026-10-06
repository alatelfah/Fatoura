import { expect, type APIRequestContext, type Page } from '@playwright/test';

export const ADMIN = { email: 'admin@fatoura.local', password: 'Admin@12345' };
export const run = Date.now().toString(36);

export async function apiToken(request: APIRequestContext, email = ADMIN.email, password = ADMIN.password): Promise<string> {
  const r = await request.post('/api/auth/login', { data: { email, password, client: 'Mobile' } });
  expect(r.ok(), await r.text()).toBeTruthy();
  return (await r.json()).accessToken as string;
}

export async function api<T = unknown>(request: APIRequestContext, token: string, method: 'GET' | 'POST' | 'PUT', url: string, data?: unknown): Promise<T> {
  const r = await request.fetch(url, { method, data, headers: { Authorization: `Bearer ${token}` } });
  expect(r.ok(), `${method} ${url}: ${r.status()} ${await r.text()}`).toBeTruthy();
  const text = await r.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

/** Company details from the reference invoice, so documents can be issued. */
export async function configureCompany(request: APIRequestContext, token: string) {
  await api(request, token, 'PUT', '/api/settings', {
    name: 'HRS TECHNICAL SERVICE LLC',
    address: 'Business Bay, Dubai',
    emirate: 'Dubai',
    phone: '+971547220420',
    email: 'info@hrstechnical.com',
    website: 'www.hrstechnical.com',
    trn: '105386581000003',
    vatRate: 0.05,
    paymentTerms: '30% payment at the time of confirmation of order 60% during progress and balance 10% after completion of work',
    completionOfWork: 'Within in 30 days after confirmation',
    notes: 'The above quoted price is inclusive of fixing material and labor cost.',
    closingText: 'We hope that you will find our price most competitive.',
    allowNegativeStock: true,
    quotationValidityDays: 30,
  });
}

export async function createCashier(request: APIRequestContext, token: string) {
  const email = `cashier-${run}-${Math.random().toString(36).slice(2, 6)}@fatoura.local`;
  const password = 'Cashier@12345';
  await api(request, token, 'POST', '/api/users', { email, displayName: `Cashier ${run}`, password, role: 'Cashier' });
  return { email, password };
}

export async function login(page: Page, email = ADMIN.email, password = ADMIN.password) {
  await page.goto('/login');
  await page.getByTestId('login-email').fill(email);
  await page.getByTestId('login-password').fill(password);
  await page.getByTestId('login-submit').click();
  await page.waitForURL('**/dashboard');
}

/** Picks an option in an antd Select identified by test id, searching by text. */
export async function pick(page: Page, testId: string, text: string) {
  await page.getByTestId(testId).click();
  await page.keyboard.type(text);
  await page.locator('.ant-select-item-option', { hasText: text }).first().click();
}
