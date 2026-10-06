import { FlatList } from 'react-native';
import { List } from 'react-native-paper';
import { Guard } from '../components/Guard';
import { $api } from '../lib/api';

export default function SuppliersScreen() {
  const { data, refetch, isRefetching } = $api.useQuery('get', '/api/suppliers', { params: { query: { pageSize: 100 } } });
  return (
    <Guard admin>
      <FlatList
        data={data?.items ?? []}
        keyExtractor={(s) => String(s.id)}
        refreshing={isRefetching}
        onRefresh={refetch}
        renderItem={({ item }) => <List.Item title={item.name} description={[item.phone, item.trn && `TRN ${item.trn}`].filter(Boolean).join(' · ')} left={(p) => <List.Icon {...p} icon="store-outline" />} />}
      />
    </Guard>
  );
}
