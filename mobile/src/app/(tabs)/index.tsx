import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { View } from 'react-native';
import { Button, Card, List, Text } from 'react-native-paper';
import { $api, type Schemas } from '../../lib/api';
import { useAuth } from '../../lib/auth';
import { formatDate, formatQty } from '../../lib/format';
import { Kpi, Loading, Ltr, Money, Screen } from '../../components/ui';

export default function DashboardScreen() {
  const { isAdmin } = useAuth();
  return isAdmin ? <AdminDashboard /> : <CashierDashboard />;
}

function Recent({ invoices }: { invoices: Schemas['RecentInvoiceDto'][] }) {
  return (
    <>
      {invoices.map((i) => (
        <List.Item
          key={i.id}
          title={i.clientName}
          description={`${i.number} · ${formatDate(i.date)}`}
          right={() => <View style={{ justifyContent: 'center' }}><Money value={i.total} currency={i.currency} /></View>}
          onPress={() => router.push(`/invoice/${i.id}`)}
        />
      ))}
    </>
  );
}

function AdminDashboard() {
  const { t } = useTranslation();
  const { data, isLoading, refetch, isRefetching } = $api.useQuery('get', '/api/dashboard/admin');
  if (isLoading || !data) return <Loading />;
  return (
    <Screen refreshing={isRefetching} onRefresh={refetch}>
      <Text variant="labelLarge">{t('dashboard.yearToDate')}: {formatDate(data.period.from)} – {formatDate(data.period.to)}</Text>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 12 }}>
        <Kpi title={t('dashboard.sales')} value={data.sales} testID="kpi-sales" />
        <Kpi title={t('dashboard.purchases')} value={data.purchases} />
        <Kpi title={t('dashboard.netProfit')} value={data.netProfit} />
        <Kpi title={t('dashboard.openBalance')} value={data.openInvoiceBalance} />
      </View>
      <Card>
        <Card.Title title={`${t('dashboard.openInvoices')}: ${data.openInvoiceCount} · ${t('dashboard.openQuotations')}: ${data.openQuotationCount}`} titleNumberOfLines={2} />
      </Card>
      <Card>
        <Card.Title title={t('dashboard.lowStock')} />
        <Card.Content>
          {data.lowStockItems.length === 0 ? (
            <Text style={{ opacity: 0.7 }}>{t('dashboard.noLowStock')}</Text>
          ) : (
            data.lowStockItems.map((i) => (
              <List.Item key={i.id} title={i.name} right={() => <Ltr style={{ color: i.stockQty <= 0 ? '#c62828' : '#ef6c00', alignSelf: 'center' }}>{formatQty(i.stockQty)}</Ltr>} />
            ))
          )}
        </Card.Content>
      </Card>
      <Card>
        <Card.Title title={t('dashboard.monthly')} titleNumberOfLines={2} />
        <Card.Content>
          {data.monthly.slice(-6).map((m) => (
            <View key={m.month} style={{ flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 2 }}>
              <Ltr>{m.month}</Ltr>
              <Money value={m.sales} />
              <Money value={m.purchases} />
            </View>
          ))}
        </Card.Content>
      </Card>
      <Card>
        <Card.Title title={t('dashboard.recentInvoices')} />
        <Recent invoices={data.recentInvoices} />
      </Card>
    </Screen>
  );
}

function CashierDashboard() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { data, isLoading, refetch, isRefetching } = $api.useQuery('get', '/api/dashboard/cashier');
  if (isLoading || !data) return <Loading />;
  return (
    <Screen refreshing={isRefetching} onRefresh={refetch}>
      <Text variant="titleMedium">{t('dashboard.welcome', { name: user?.displayName })}</Text>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 12 }}>
        <Kpi title={`${t('dashboard.todaySales')} (${t('dashboard.inclVat')})`} value={data.shiftSalesTotal} testID="kpi-shift-total" />
        <Kpi title={t('dashboard.shiftInvoices')} value={data.shiftInvoiceCount} />
      </View>
      <Button mode="contained" icon="file-plus-outline" onPress={() => router.push('/invoice/new')} testID="quick-new-invoice">
        {t('dashboard.newInvoice')}
      </Button>
      <Button mode="outlined" icon="file-document-edit-outline" onPress={() => router.push('/quotation/new')}>
        {t('dashboard.newQuotation')}
      </Button>
      <Card>
        <Card.Title title={t('dashboard.myRecentInvoices')} />
        <Recent invoices={data.recentInvoices} />
      </Card>
    </Screen>
  );
}
