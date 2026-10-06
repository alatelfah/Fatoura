import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FlatList, View } from 'react-native';
import { Chip, List, Searchbar } from 'react-native-paper';
import { Guard } from '../components/Guard';
import { Ltr, Money } from '../components/ui';
import { $api } from '../lib/api';
import { formatQty } from '../lib/format';

export default function ItemsScreen() {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');
  const [lowStock, setLowStock] = useState(false);
  const { data, refetch, isRefetching } = $api.useQuery('get', '/api/items', { params: { query: { search: search || undefined, lowStock: lowStock || undefined, pageSize: 100 } } });
  return (
    <Guard>
      <View style={{ flex: 1 }}>
        <Searchbar placeholder={t('common.search')} value={search} onChangeText={setSearch} style={{ margin: 12 }} />
        <View style={{ flexDirection: 'row', paddingHorizontal: 12 }}>
          <Chip selected={lowStock} onPress={() => setLowStock((v) => !v)}>{t('item.lowStockOnly')}</Chip>
        </View>
        <FlatList
          data={data?.items ?? []}
          keyExtractor={(i) => String(i.id)}
          refreshing={isRefetching}
          onRefresh={refetch}
          renderItem={({ item }) => (
            <List.Item
              title={item.name}
              description={`${t(`item.${item.type}`)} · ${t(`item.${item.taxCategory}`)}`}
              right={() => (
                <View style={{ alignItems: 'flex-end', justifyContent: 'center' }}>
                  <Money value={item.unitPrice} bold />
                  {item.trackStock && <Ltr style={{ color: item.isLowStock ? '#ef6c00' : '#2e7d32' }}>{t('item.stock')}: {formatQty(item.stockQty)}</Ltr>}
                </View>
              )}
            />
          )}
        />
      </View>
    </Guard>
  );
}
