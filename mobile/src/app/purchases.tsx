import { FlatList, View } from 'react-native';
import { List } from 'react-native-paper';
import { Guard } from '../components/Guard';
import { Money } from '../components/ui';
import { $api } from '../lib/api';
import { formatDate } from '../lib/format';

export default function PurchasesScreen() {
  const { data, refetch, isRefetching } = $api.useQuery('get', '/api/purchases', { params: { query: { pageSize: 50 } } });
  return (
    <Guard admin>
      <FlatList
        data={data?.items ?? []}
        keyExtractor={(p) => String(p.id)}
        refreshing={isRefetching}
        onRefresh={refetch}
        renderItem={({ item }) => (
          <List.Item
            title={item.supplierName}
            description={`${item.number} · ${item.supplierInvoiceNo || '—'} · ${formatDate(item.date)}`}
            right={() => <View style={{ justifyContent: 'center' }}><Money value={item.total} currency={item.currency} bold /></View>}
          />
        )}
      />
    </Guard>
  );
}
