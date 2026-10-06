import { router } from 'expo-router';
import { FlatList, View } from 'react-native';
import { List } from 'react-native-paper';
import { Guard } from '../components/Guard';
import { Money } from '../components/ui';
import { $api } from '../lib/api';
import { formatDate } from '../lib/format';

export default function CreditNotesScreen() {
  const { data, refetch, isRefetching } = $api.useQuery('get', '/api/credit-notes', { params: { query: { pageSize: 50 } } });
  return (
    <Guard>
      <FlatList
        data={data?.items ?? []}
        keyExtractor={(c) => String(c.id)}
        refreshing={isRefetching}
        onRefresh={refetch}
        renderItem={({ item }) => (
          <List.Item
            title={item.clientName}
            description={`${item.number} · ${item.invoiceNumber} · ${formatDate(item.date)}`}
            onPress={() => router.push(`/credit-note/${item.id}`)}
            right={() => <View style={{ justifyContent: 'center' }}><Money value={item.total} bold /></View>}
          />
        )}
      />
    </Guard>
  );
}
