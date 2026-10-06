import { useQueryClient } from '@tanstack/react-query';
import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { View } from 'react-native';
import { Button, Card, Switch, Text, TextInput } from 'react-native-paper';
import { Guard } from '../../components/Guard';
import { ErrorText, Loading, Screen } from '../../components/ui';
import { $api, fetchClient } from '../../lib/api';
import { formatQty, parseNumber } from '../../lib/format';

export default function NewCreditNoteScreen() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const invoiceId = Number(useLocalSearchParams<{ invoiceId: string }>().invoiceId);
  const { data: inv } = $api.useQuery('get', '/api/invoices/{id}', { params: { path: { id: invoiceId } } });
  const [qty, setQty] = useState<Record<number, string>>({});
  const [reason, setReason] = useState('');
  const [returnToStock, setReturnToStock] = useState(true);
  const [error, setError] = useState<unknown>(null);
  if (!inv) return <Guard><Loading /></Guard>;

  const save = async () => {
    const lines = inv.lines.map((l) => ({ invoiceLineId: l.id, quantity: parseNumber(qty[l.id] ?? '') ?? 0 })).filter((l) => l.quantity > 0);
    const { data, error: e } = await fetchClient.POST('/api/credit-notes', { body: { invoiceId, date: null, reason, returnToStock, lines } });
    if (e || !data) return setError(e);
    await queryClient.invalidateQueries();
    router.replace(`/credit-note/${data.id}`);
  };

  return (
    <Guard>
      <Screen>
        <Text variant="titleMedium">{t('creditNote.againstInvoice')}: {inv.number}</Text>
        {inv.lines.map((l) => (
          <Card key={l.id}>
            <Card.Content style={{ gap: 8 }}>
              <Text>{l.description}</Text>
              <Text style={{ opacity: 0.7 }}>{t('creditNote.remaining')}: {formatQty(l.quantity - l.creditedQuantity)}</Text>
              <TextInput label={t('creditNote.creditQty')} value={qty[l.id] ?? ''} onChangeText={(v) => setQty({ ...qty, [l.id]: v })} keyboardType="decimal-pad" mode="outlined" dense disabled={l.quantity - l.creditedQuantity <= 0} />
            </Card.Content>
          </Card>
        ))}
        <TextInput label={t('creditNote.reason')} value={reason} onChangeText={setReason} mode="outlined" />
        <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
          <Text>{t('creditNote.returnToStock')}</Text>
          <Switch value={returnToStock} onValueChange={setReturnToStock} />
        </View>
        <ErrorText error={error} />
        <Button mode="contained" onPress={save} disabled={!reason.trim()}>{t('common.create')}</Button>
      </Screen>
    </Guard>
  );
}
