import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FlatList, View } from 'react-native';
import { Button, Card, Divider, IconButton, List, Modal, Portal, Searchbar, SegmentedButtons, Text, TextInput } from 'react-native-paper';
import { previewDocument, type TaxCategory } from '@fatoura/shared';
import { $api, type Schemas } from '../lib/api';
import { parseNumber } from '../lib/format';
import { Money } from './ui';

export interface EditableLine {
  key: number;
  itemId: number | null;
  description: string;
  qty: string;
  price: string;
  tax: TaxCategory;
}

let nextKey = 1;
export const newLine = (): EditableLine => ({ key: nextKey++, itemId: null, description: '', qty: '1', price: '', tax: 'Standard' });

export function toRequests(lines: EditableLine[]): Schemas['DocumentLineRequest'][] {
  return lines.map((l) => ({
    itemId: l.itemId,
    description: l.description.trim(),
    quantity: parseNumber(l.qty) ?? 0,
    unitPrice: parseNumber(l.price) ?? 0,
    taxCategory: l.tax,
  }));
}

/** Searchable picker in a modal (clients or items). */
export function Picker<T extends { id: number; name: string }>({
  visible, onDismiss, onPick, title, items, search, onSearch, describe,
}: { visible: boolean; onDismiss: () => void; onPick: (item: T) => void; title: string; items: T[]; search: string; onSearch: (s: string) => void; describe?: (item: T) => string }) {
  return (
    <Portal>
      <Modal visible={visible} onDismiss={onDismiss} contentContainerStyle={{ backgroundColor: 'white', margin: 16, borderRadius: 12, maxHeight: '80%', padding: 8 }}>
        <Text variant="titleMedium" style={{ padding: 8 }}>{title}</Text>
        <Searchbar value={search} onChangeText={onSearch} placeholder={title} />
        <FlatList
          data={items}
          keyExtractor={(i) => String(i.id)}
          keyboardShouldPersistTaps="handled"
          renderItem={({ item }) => <List.Item title={item.name} description={describe?.(item)} onPress={() => { onPick(item); onDismiss(); }} />}
        />
      </Modal>
    </Portal>
  );
}

export function ClientField({ client, onChange }: { client: { id: number; name: string } | null; onChange: (c: { id: number; name: string }) => void }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const { data } = $api.useQuery('get', '/api/clients', { params: { query: { search: search || undefined, pageSize: 50 } } }, { enabled: open });
  return (
    <>
      <List.Item
        title={client?.name ?? t('mobile.pickClient')}
        description={t('doc.client')}
        left={(p) => <List.Icon {...p} icon="account" />}
        right={(p) => <List.Icon {...p} icon="chevron-down" />}
        onPress={() => setOpen(true)}
        style={{ backgroundColor: 'white', borderRadius: 8 }}
        testID="client-field"
      />
      <Picker visible={open} onDismiss={() => setOpen(false)} onPick={onChange} title={t('mobile.pickClient')} items={data?.items ?? []} search={search} onSearch={setSearch} describe={(c) => c.trn} />
    </>
  );
}

const TAXES: TaxCategory[] = ['Standard', 'ZeroRated', 'Exempt'];

/** Line editor with live per-line VAT and totals using the same calculator as the server. */
export function LinesEditor({ lines, onChange, vatRate }: { lines: EditableLine[]; onChange: (lines: EditableLine[]) => void; vatRate: number }) {
  const { t } = useTranslation();
  const [pickFor, setPickFor] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const items = $api.useQuery('get', '/api/items', { params: { query: { search: search || undefined, pageSize: 100 } } }, { enabled: pickFor !== null });
  const totals = previewDocument(lines.map((l) => ({ qty: parseNumber(l.qty), unitPrice: parseNumber(l.price), tax: l.tax })), vatRate);
  const update = (key: number, patch: Partial<EditableLine>) => onChange(lines.map((l) => (l.key === key ? { ...l, ...patch } : l)));

  return (
    <View style={{ gap: 12 }}>
      {lines.map((line, index) => (
        <Card key={line.key} testID={`line-${index}`}>
          <Card.Content style={{ gap: 8 }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
              <Text variant="labelLarge">#{index + 1}</Text>
              <Button compact icon="package-variant" onPress={() => setPickFor(line.key)}>{t('mobile.pickItem')}</Button>
              <IconButton icon="delete-outline" onPress={() => onChange(lines.filter((l) => l.key !== line.key))} accessibilityLabel={t('mobile.removeLine')} disabled={lines.length === 1} />
            </View>
            <TextInput label={t('doc.description')} value={line.description} onChangeText={(description) => update(line.key, { description })} mode="outlined" multiline dense testID={`line-${index}-description`} />
            <View style={{ flexDirection: 'row', gap: 8 }}>
              <TextInput style={{ flex: 1 }} label={t('doc.qty')} value={line.qty} onChangeText={(qty) => update(line.key, { qty })} keyboardType="decimal-pad" mode="outlined" dense testID={`line-${index}-qty`} />
              <TextInput style={{ flex: 1.4 }} label={t('doc.unitPrice')} value={line.price} onChangeText={(price) => update(line.key, { price })} keyboardType="decimal-pad" mode="outlined" dense testID={`line-${index}-price`} />
            </View>
            <SegmentedButtons density="small" value={line.tax} onValueChange={(tax) => update(line.key, { tax: tax as TaxCategory })} buttons={TAXES.map((x) => ({ value: x, label: t(`item.${x}`) }))} />
            <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
              <Text>{t('doc.vat')}: <Money value={totals.lines[index]?.vat.toFixed(2)} /></Text>
              <Text>{t('mobile.lineTotal')}: <Money value={totals.lines[index]?.total.toFixed(2)} bold testID={`line-${index}-amount`} /></Text>
            </View>
          </Card.Content>
        </Card>
      ))}
      <Button mode="outlined" icon="plus" onPress={() => onChange([...lines, newLine()])} testID="add-line">{t('doc.addLine')}</Button>
      <Card>
        <Card.Content style={{ gap: 4 }}>
          <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}><Text>{t('doc.subTotal')}</Text><Money value={totals.subTotal.toFixed(2)} /></View>
          <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}><Text>{t('doc.vatTotal')} {Number((vatRate * 100).toFixed(2))}%</Text><Money value={totals.vatTotal.toFixed(2)} /></View>
          <Divider />
          <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}><Text style={{ fontWeight: '700' }}>{t('doc.total')}</Text><Money value={totals.total.toFixed(2)} bold testID="grand-total" /></View>
        </Card.Content>
      </Card>
      <Picker
        visible={pickFor !== null}
        onDismiss={() => setPickFor(null)}
        title={t('mobile.pickItem')}
        items={items.data?.items ?? []}
        search={search}
        onSearch={setSearch}
        describe={(i) => `${i.unitPrice.toFixed(2)} · ${t(`item.${i.taxCategory}`)}`}
        onPick={(item) => pickFor !== null && update(pickFor, { itemId: item.id, description: item.description || item.name, price: String(item.unitPrice), tax: item.taxCategory })}
      />
    </View>
  );
}
