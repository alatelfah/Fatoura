import { router } from 'expo-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FlatList, View } from 'react-native';
import { FAB, List, Searchbar } from 'react-native-paper';
import { $api } from '../../lib/api';
import { formatDate } from '../../lib/format';
import { Money, StatusChip } from '../../components/ui';

export const QUOTATION_COLORS = { Draft: '#757575', Sent: '#1565c0', Accepted: '#2e7d32', Rejected: '#c62828', Converted: '#6a1b9a' } as const;

export default function QuotationsScreen() {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');
  const { data, refetch, isRefetching } = $api.useQuery('get', '/api/quotations', { params: { query: { search: search || undefined, pageSize: 50 } } });
  return (
    <View style={{ flex: 1 }}>
      <Searchbar placeholder={t('common.search')} value={search} onChangeText={setSearch} style={{ margin: 12 }} />
      <FlatList
        data={data?.items ?? []}
        keyExtractor={(i) => String(i.id)}
        refreshing={isRefetching}
        onRefresh={refetch}
        renderItem={({ item }) => (
          <List.Item
            title={item.clientName}
            description={`${item.number} · ${formatDate(item.date)}`}
            onPress={() => router.push(`/quotation/${item.id}`)}
            right={() => (
              <View style={{ alignItems: 'flex-end', justifyContent: 'center', gap: 4 }}>
                <Money value={item.total} bold />
                <StatusChip label={t(`quotation.${item.status}`)} color={QUOTATION_COLORS[item.status]} />
              </View>
            )}
          />
        )}
      />
      <FAB icon="plus" style={{ position: 'absolute', end: 16, bottom: 16 }} onPress={() => router.push('/quotation/new')} />
    </View>
  );
}
