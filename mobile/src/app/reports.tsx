import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, DataTable, SegmentedButtons, Text } from 'react-native-paper';
import { Guard } from '../components/Guard';
import { Loading, Money, Row, Screen } from '../components/ui';
import { $api } from '../lib/api';
import { useAuth } from '../lib/auth';
import { formatDate } from '../lib/format';

type Period = 'month' | 'quarter' | 'year';

function range(period: Period): { from: string; to: string } {
  const now = new Date();
  const y = now.getFullYear();
  const m = now.getMonth();
  const start = period === 'month' ? new Date(y, m, 1) : period === 'quarter' ? new Date(y, m - (m % 3), 1) : new Date(y, 0, 1);
  const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  return { from: iso(start), to: iso(now) };
}

/** Read-only reports; cashiers see their own sales, admins also P&L and VAT (exports are in the web app). */
export default function ReportsScreen() {
  const { t } = useTranslation();
  const { isAdmin } = useAuth();
  const [period, setPeriod] = useState<Period>('month');
  const q = range(period);
  const sales = $api.useQuery('get', '/api/reports/sales', { params: { query: q } });
  const pl = $api.useQuery('get', '/api/reports/profit-loss', { params: { query: q } }, { enabled: isAdmin });
  const vat = $api.useQuery('get', '/api/reports/vat', { params: { query: q } }, { enabled: isAdmin });

  return (
    <Guard>
      <Screen>
        <SegmentedButtons value={period} onValueChange={(v) => setPeriod(v as Period)} buttons={[
          { value: 'month', label: t('common.period') + ' · M' },
          { value: 'quarter', label: 'Q' },
          { value: 'year', label: 'Y' },
        ]} />
        <Text style={{ opacity: 0.7 }}>{formatDate(q.from)} – {formatDate(q.to)}</Text>
        {!sales.data ? <Loading /> : (
          <Card>
            <Card.Title title={t('reports.sales')} />
            <Card.Content>
              <Row label={t('reports.invoices')}><Text>{sales.data.summary.invoiceCount}</Text></Row>
              <Row label={t('reports.creditNotes')}><Money value={-sales.data.summary.creditNotesTotal} /></Row>
              <Row label={t('reports.netSales')}><Money value={sales.data.summary.netSales} /></Row>
              <Row label={t('reports.netVat')}><Money value={sales.data.summary.netVat} /></Row>
              <Row label={t('reports.netTotal')}><Money value={sales.data.summary.netTotal} bold /></Row>
            </Card.Content>
          </Card>
        )}
        {isAdmin && pl.data && (
          <Card>
            <Card.Title title={t('reports.profitLoss')} />
            <Card.Content>
              <Row label={t('reports.netSales')}><Money value={pl.data.netSales} /></Row>
              <Row label={t('reports.totalCosts')}><Money value={pl.data.totalPurchases} /></Row>
              <Row label={t('reports.netProfit')}><Money value={pl.data.netProfit} bold /></Row>
            </Card.Content>
          </Card>
        )}
        {isAdmin && vat.data && (
          <Card>
            <Card.Title title={t('reports.vat')} />
            <DataTable>
              {vat.data.boxes.filter((b) => b.amount !== 0 || b.vat !== 0).map((b) => (
                <DataTable.Row key={b.box}>
                  <DataTable.Cell style={{ flex: 0.4 }}>{b.box}</DataTable.Cell>
                  <DataTable.Cell numeric><Money value={b.amount} /></DataTable.Cell>
                  <DataTable.Cell numeric><Money value={b.vat} /></DataTable.Cell>
                </DataTable.Row>
              ))}
            </DataTable>
            <Card.Content>
              <Row label={vat.data.netVatPayable >= 0 ? t('reports.payable') : t('reports.refundable')}><Money value={Math.abs(vat.data.netVatPayable)} bold /></Row>
            </Card.Content>
          </Card>
        )}
      </Screen>
    </Guard>
  );
}
