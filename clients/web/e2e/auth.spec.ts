import { expect, test } from '@playwright/test';
import { apiToken, createCashier, login } from './helpers';

test('admin signs in, sees admin menu and the session survives a reload', async ({ page }) => {
  await login(page);
  const menu = page.getByTestId('main-menu');
  await expect(menu).toContainText('Settings');
  await expect(menu).toContainText('Purchases');
  await page.reload();
  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByTestId('kpi-sales')).toBeVisible();
});

test('wrong password shows an error', async ({ page }) => {
  await page.goto('/login');
  await page.getByTestId('login-email').fill('admin@fatoura.local');
  await page.getByTestId('login-password').fill('not-the-password');
  await page.getByTestId('login-submit').click();
  await expect(page.getByTestId('login-error')).toBeVisible();
});

test('cashier lands on the cashier dashboard and is blocked from admin pages', async ({ page, request }) => {
  const cashier = await createCashier(request, await apiToken(request));
  await login(page, cashier.email, cashier.password);
  await expect(page.getByTestId('quick-new-invoice')).toBeVisible();
  const menu = page.getByTestId('main-menu');
  await expect(menu).not.toContainText('Settings');
  await expect(menu).not.toContainText('Purchases');
  for (const path of ['/settings', '/users', '/purchases', '/suppliers']) {
    await page.goto(path);
    await expect(page.getByTestId('forbidden')).toBeVisible();
  }
});

test('switching to Arabic turns the layout right-to-left', async ({ page }) => {
  await login(page);
  await page.getByTestId('language-switch').click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
  await expect(page.getByTestId('main-menu')).toContainText('لوحة القيادة');
  await page.screenshot({ path: 'test-results/dashboard-ar.png', fullPage: true });
  await page.getByTestId('language-switch').click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
});
