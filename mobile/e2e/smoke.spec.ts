import { expect, test } from '@playwright/test';

const ADMIN = { email: 'admin@fatoura.local', password: 'Admin@12345' };

async function login(page: import('@playwright/test').Page) {
  await page.goto('/');
  await page.getByTestId('login-email').fill(ADMIN.email);
  await page.getByTestId('login-password').fill(ADMIN.password);
  await page.getByTestId('login-submit').click();
  await expect(page.getByTestId('kpi-sales')).toBeVisible();
}

test('admin signs in and sees the dashboard and invoices', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await login(page);
  await page.getByRole('tab', { name: /Tax Invoices/ }).click();
  await expect(page.getByTestId('invoice-new')).toBeVisible();
  await page.screenshot({ path: 'test-results/mobile-invoices.png' });
  expect(errors).toEqual([]);
});

test('admin issues an invoice from the phone layout', async ({ page, request }) => {
  const token = (await (await request.post('/api/auth/login', { data: { ...ADMIN, client: 'Mobile' } })).json()).accessToken as string;
  const name = `Mobile client ${Date.now().toString(36)}`;
  await request.put('/api/settings', {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      name: 'HRS TECHNICAL SERVICE LLC', address: 'Business Bay, Dubai', emirate: 'Dubai', phone: '+971547220420', email: 'info@hrstechnical.com',
      website: 'www.hrstechnical.com', trn: '105386581000003', vatRate: 0.05, paymentTerms: '', completionOfWork: '', notes: '', closingText: '',
      allowNegativeStock: true, quotationValidityDays: 30,
    },
  });
  await request.post('/api/clients', { headers: { Authorization: `Bearer ${token}` }, data: { name } });

  await login(page);
  await page.goto('/invoice/new');
  await page.getByTestId('client-field').click();
  await page.getByPlaceholder('Choose client').fill(name);
  await page.getByText(name, { exact: true }).click();
  await page.getByTestId('line-0-description').fill('Supply & installation per piece');
  await page.getByTestId('line-0-qty').fill('20');
  await page.getByTestId('line-0-price').fill('250');
  await expect(page.getByTestId('grand-total')).toHaveText('5,250.00');
  await page.getByTestId('document-submit').click();
  await expect(page.getByTestId('doc-total')).toHaveText('5,250.00');
  await expect(page.getByTestId('invoice-balance')).toHaveText('0.00');
  await page.screenshot({ path: 'test-results/mobile-invoice.png' });
});

test('switching to Arabic sets right-to-left', async ({ page }) => {
  await login(page);
  await page.getByRole('tab', { name: /More/ }).click();
  await page.getByTestId('language-toggle').click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await page.screenshot({ path: 'test-results/mobile-more-ar.png' });
  await page.getByTestId('language-toggle').click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
});
