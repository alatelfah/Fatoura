import { Skeleton } from 'antd';
import { lazy, Suspense, type ComponentType, type ReactNode } from 'react';
import { createBrowserRouter, Navigate } from 'react-router';
import { AdminOnly, NotFound, RequireAuth } from './auth/guards';
import { AppLayout } from './layout/AppLayout';
import { LoginPage } from './pages/LoginPage';

/** Route-level code splitting: each page module loads on first visit. */
function page<T>(load: () => Promise<T>, name: keyof T, adminOnly = false): ReactNode {
  const Component = lazy(async () => ({ default: (await load())[name] as ComponentType }));
  const element = (
    <Suspense fallback={<Skeleton active />}>
      <Component />
    </Suspense>
  );
  return adminOnly ? <AdminOnly>{element}</AdminOnly> : element;
}

const dashboard = () => import('./pages/dashboard/DashboardPage');
const quotations = () => import('./pages/quotations/QuotationPages');
const invoices = () => import('./pages/invoices/InvoicePages');
const creditNotes = () => import('./pages/creditNotes/CreditNotePages');
const contacts = () => import('./pages/contacts/ContactsPage');
const items = () => import('./pages/items/ItemsPage');
const purchases = () => import('./pages/purchases/PurchasePages');
const reports = () => import('./pages/reports/ReportsPage');
const settings = () => import('./pages/settings/SettingsPage');
const users = () => import('./pages/users/UsersPage');

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppLayout />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <Navigate to="/dashboard" replace /> },
      { path: 'dashboard', element: page(dashboard, 'DashboardPage') },
      { path: 'quotations', element: page(quotations, 'QuotationsPage') },
      { path: 'quotations/new', element: page(quotations, 'QuotationNewPage') },
      { path: 'quotations/:id', element: page(quotations, 'QuotationViewPage') },
      { path: 'quotations/:id/edit', element: page(quotations, 'QuotationEditPage') },
      { path: 'invoices', element: page(invoices, 'InvoicesPage') },
      { path: 'invoices/new', element: page(invoices, 'InvoiceNewPage') },
      { path: 'invoices/:id', element: page(invoices, 'InvoiceViewPage') },
      { path: 'invoices/:id/edit', element: page(invoices, 'InvoiceEditPage', true) },
      { path: 'credit-notes', element: page(creditNotes, 'CreditNotesPage') },
      { path: 'credit-notes/:id', element: page(creditNotes, 'CreditNoteViewPage') },
      { path: 'clients', element: page(contacts, 'ClientsPage') },
      { path: 'suppliers', element: page(contacts, 'SuppliersPage', true) },
      { path: 'items', element: page(items, 'ItemsPage') },
      { path: 'purchases', element: page(purchases, 'PurchasesPage', true) },
      { path: 'purchases/new', element: page(purchases, 'PurchaseFormPage', true) },
      { path: 'purchases/:id', element: page(purchases, 'PurchaseViewPage', true) },
      { path: 'purchases/:id/edit', element: page(purchases, 'PurchaseFormPage', true) },
      { path: 'reports', element: page(reports, 'ReportsPage') },
      { path: 'settings', element: page(settings, 'SettingsPage', true) },
      { path: 'users', element: page(users, 'UsersPage', true) },
      { path: '*', element: <NotFound /> },
    ],
  },
]);
