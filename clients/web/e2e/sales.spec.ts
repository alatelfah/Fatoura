import { expect, test } from '@playwright/test';
import { api, apiToken, configureCompany, createCashier, login, pick, run } from './helpers';

test.beforeAll(async ({ request }) => {
  await configureCompany(request, await apiToken(request));
});

test('client with an invalid TRN is rejected, valid one is saved', async ({ page }) => {
  await login(page);
  await page.goto('/clients');
  await page.getByTestId('contact-new').click();
  await page.getByTestId('contact-name').fill(`Bad TRN ${run}`);
  await page.getByTestId('contact-trn').fill('12345');
  await page.getByTestId('contact-save').click();
  await expect(page.getByText('TRN must be 15 digits starting with 10')).toBeVisible();
  await page.getByTestId('contact-trn').fill('105325228200003');
  await page.getByTestId('contact-save').click();
  await expect(page.getByText(`Bad TRN ${run}`)).toBeVisible();
});

test('quotation with the reference lines converts to a tax invoice and credit note', async ({ page, request }) => {
  const token = await apiToken(request);
  const clientName = `Aura Suites Downtown ${run}`;
  await api(request, token, 'POST', '/api/clients', { name: clientName, address: 'Marasi Drive 6B Street- Business Bay Dubai', trn: '105325228200003' });

  await login(page);
  await page.goto('/quotations/new');
  await pick(page, 'clients-select', clientName);

  await page.getByTestId('line-0-description').fill('BOMPANI BUILTIN DISHWAHER Stainless steel 14 Place settings');
  await page.getByTestId('line-0-qty').fill('20');
  await page.getByTestId('line-0-price').fill('2050');
  await page.getByTestId('add-line').click();
  await page.getByTestId('line-1-description').fill('Supply & installation per piece');
  await page.getByTestId('line-1-qty').fill('20');
  await page.getByTestId('line-1-price').fill('250');
  await page.getByTestId('line-1-price').blur();

  // Live preview uses the same math as the server: 46,000.00 + 2,300.00 VAT.
  await expect(page.getByTestId('line-0-amount')).toHaveText('43,050.00');
  await expect(page.getByTestId('grand-total')).toHaveText('48,300.00');
  await page.getByTestId('document-submit').click();

  await expect(page.getByTestId('doc-total')).toHaveText('48,300.00');
  await expect(page.getByTestId('doc-number')).toContainText('QUO/');

  await page.getByTestId('quotation-convert').click();
  await page.getByRole('button', { name: 'Confirm' }).click();
  await page.waitForURL(/\/invoices\/\d+$/);
  await expect(page.getByTestId('doc-number')).toContainText('INV/');
  await expect(page.getByTestId('doc-total')).toHaveText('48,300.00');
  await expect(page.getByTestId('invoice-balance')).toHaveText('48,300.00');

  // Server-rendered PDF for the same invoice.
  const invoiceId = page.url().split('/').pop();
  const pdf = await request.get(`/api/invoices/${invoiceId}/pdf`, { headers: { Authorization: `Bearer ${token}` } });
  expect(pdf.headers()['content-type']).toBe('application/pdf');
  expect((await pdf.body()).subarray(0, 5).toString()).toBe('%PDF-');

  // Credit two dishwashers.
  await page.getByTestId('invoice-credit-note').click();
  await page.getByTestId('credit-qty-0').fill('2');
  await page.getByTestId('credit-reason').fill('2 units returned damaged');
  await page.getByTestId('credit-confirm').click();
  await page.waitForURL(/\/credit-notes\/\d+$/);
  await expect(page.getByTestId('doc-number')).toContainText('CN/');
  await expect(page.getByTestId('doc-total')).toHaveText('4,305.00');
});

test('invoice with a 10% discount taxes the discounted value', async ({ page, request }) => {
  const token = await apiToken(request);
  const clientName = `Discount Client ${run}`;
  await api(request, token, 'POST', '/api/clients', { name: clientName });

  await login(page);
  await page.goto('/invoices/new');
  await pick(page, 'clients-select', clientName);
  await page.getByTestId('line-0-description').fill('Dishwasher');
  await page.getByTestId('line-0-qty').fill('20');
  await page.getByTestId('line-0-price').fill('2050');
  await page.getByTestId('add-line').click();
  await page.getByTestId('line-1-description').fill('Installation');
  await page.getByTestId('line-1-qty').fill('20');
  await page.getByTestId('line-1-price').fill('250');
  await pick(page, 'discount-kind', 'Percent (%)');
  await page.getByTestId('discount-value').fill('10');
  await page.getByTestId('discount-value').blur();

  // 46,000.00 − 4,600.00 = 41,400.00, + 2,070.00 VAT.
  await expect(page.getByTestId('discount-amount')).toHaveText('-4,600.00');
  await expect(page.getByTestId('grand-total')).toHaveText('43,470.00');
  await page.getByTestId('document-submit').click();

  await page.waitForURL(/\/invoices\/\d+$/);
  await expect(page.getByTestId('doc-discount')).toHaveText('-4,600.00');
  await expect(page.getByTestId('doc-total')).toHaveText('43,470.00');
  await expect(page.getByTestId('invoice-balance')).toHaveText('0.00');
});

test('cashier issues a paid invoice from the dashboard but cannot void it', async ({ page, request }) => {
  const token = await apiToken(request);
  const clientName = `Walk-in ${run}`;
  await api(request, token, 'POST', '/api/clients', { name: clientName });
  const cashier = await createCashier(request, token);

  await login(page, cashier.email, cashier.password);
  await page.getByTestId('quick-new-invoice').click();
  await expect(page.getByTestId('document-date')).toBeDisabled();
  await pick(page, 'clients-select', clientName);
  await page.getByTestId('line-0-description').fill('Repair visit');
  await page.getByTestId('line-0-qty').fill('2');
  await page.getByTestId('line-0-price').fill('150');
  await page.getByTestId('line-0-price').blur();
  await expect(page.getByTestId('grand-total')).toHaveText('315.00');
  await page.getByTestId('document-submit').click();

  await page.waitForURL(/\/invoices\/\d+$/);
  await expect(page.getByTestId('invoice-balance')).toHaveText('0.00');
  await expect(page.getByTestId('invoice-void')).toHaveCount(0);
  await expect(page.getByTestId('invoice-credit-note')).toBeVisible();

  await page.goto('/dashboard');
  await expect(page.getByTestId('kpi-shift-count')).toContainText('1');
});

test('admin voids an invoice and it keeps its number', async ({ page, request }) => {
  const token = await apiToken(request);
  const client = await api<{ id: number }>(request, token, 'POST', '/api/clients', { name: `Void ${run}` });
  const issued = await api<{ invoice: { id: number; number: string } }>(request, token, 'POST', '/api/invoices', {
    clientId: client.id,
    lines: [{ description: 'Mistake', quantity: 1, unitPrice: 100, taxCategory: 'Standard' }],
  });

  await login(page);
  await page.goto(`/invoices/${issued.invoice.id}`);
  await page.getByTestId('invoice-void').click();
  await page.getByTestId('void-reason').fill('Issued to the wrong client');
  await page.getByTestId('void-confirm').click();
  await expect(page.getByText('Void: Issued to the wrong client')).toBeVisible();
  await expect(page.getByTestId('doc-number')).toHaveText(issued.invoice.number);
});
