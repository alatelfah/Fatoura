import { router } from 'expo-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FlatList, View } from 'react-native';
import { Chip, FAB, List, Searchbar } from 'react-native-paper';
import { $api } from '../../lib/api';
import { formatDate } from '../../lib/format';
import { Money, StatusChip } from '../../components/ui';

export default function InvoicesScreen() {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');
  const [unpaidOnly, setUnpaidOnly] = useState(false);
  const { data, refetch, isRefetching } = $api.useQuery('get', '/api/invoices', { params: { query: { search: search || undefined, unpaidOnly: unpaidOnly || undefined, pageSize: 50 } } });
  return (
    <View style={{ flex: 1 }}>
      <Searchbar placeholder={t('common.search')} value={search} onChangeText={setSearch} style={{ margin: 12 }} />
      <View style={{ flexDirection: 'row', paddingHorizontal: 12 }}>
        <Chip selected={unpaidOnly} onPress={() => setUnpaidOnly((v) => !v)}>{t('invoice.unpaidOnly')}</Chip>
      </View>
      <FlatList
        data={data?.items ?? []}
        keyExtractor={(i) => String(i.id)}
        refreshing={isRefetching}
        onRefresh={refetch}
        renderItem={({ item }) => (
          <List.Item
            title={item.clientName}
            description={`${item.number} · ${formatDate(item.date)}`}
            onPress={() => router.push(`/invoice/${item.id}`)}
            right={() => (
              <View style={{ alignItems: 'flex-end', justifyContent: 'center', gap: 4 }}>
                <Money value={item.total} currency={item.currency} bold />
                {item.status === 'Void' ? <StatusChip label={t('invoice.Void')} color="#c62828" /> : item.balance > 0 ? <StatusChip label={t('doc.balance')} color="#ef6c00" /> : <StatusChip label={t('invoice.paidInFull')} color="#2e7d32" />}
              </View>
            )}
          />
        )}
      />
      <FAB icon="plus" style={{ position: 'absolute', end: 16, bottom: 16 }} onPress={() => router.push('/invoice/new')} testID="invoice-new" />
    </View>
  );
}
