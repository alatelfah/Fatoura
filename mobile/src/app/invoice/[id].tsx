import { useQueryClient } from '@tanstack/react-query';
import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { View } from 'react-native';
import { Button, Card, Dialog, List, Portal, SegmentedButtons, Text, TextInput } from 'react-native-paper';
import { DocumentDetails } from '../../components/DocumentDetails';
import { Guard } from '../../components/Guard';
import { ErrorText, Loading, Ltr, Money, Screen, StatusChip } from '../../components/ui';
import { $api, fetchClient, sharePdf, type Schemas } from '../../lib/api';
import { formatDate, parseNumber } from '../../lib/format';
import { METHODS } from './new';

export default function InvoiceScreen() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const id = Number(useLocalSearchParams<{ id: string }>().id);
  const { data: inv, refetch, isRefetching } = $api.useQuery('get', '/api/invoices/{id}', { params: { path: { id } } });
  const [paying, setPaying] = useState(false);
  const [amount, setAmount] = useState('');
  const [method, setMethod] = useState<Schemas['PaymentMethod']>('Cash');
  const [error, setError] = useState<unknown>(null);
  const [sharing, setSharing] = useState(false);
  if (!inv) return <Guard><Loading /></Guard>;

  const pay = async () => {
    const { error: e } = await fetchClient.POST('/api/invoices/{id}/payments', { params: { path: { id } }, body: { amount: parseNumber(amount) ?? 0, method, date: null, reference: null } });
    if (e) return setError(e);
    setPaying(false);
    await refetch();
    await queryClient.invalidateQueries({ queryKey: ['get', '/api/invoices'] });
  };

  const share = async () => {
    setSharing(true);
    try {
      await sharePdf(`/api/invoices/${id}/pdf`, `Invoice-${inv.number.replace(/[^\w-]/g, '-')}.pdf`);
    } finally {
      setSharing(false);
    }
  };

  return (
    <Guard>
      <Screen refreshing={isRefetching} onRefresh={refetch}>
        {inv.status === 'Void' && <StatusChip label={`${t('invoice.Void')}: ${inv.voidReason}`} color="#c62828" />}
        <DocumentDetails
          number={inv.number}
          date={inv.date}
          client={inv.client}
          lines={inv.lines}
          subTotal={inv.subTotal}
          vatTotal={inv.vatTotal}
          total={inv.total}
          extra={[
            [t('doc.createdBy'), inv.createdByName],
            [t('doc.paid'), <Money key="p" value={inv.paidTotal} />],
            ...(inv.creditedTotal > 0 ? [[t('doc.credited'), <Money key="c" value={inv.creditedTotal} />] as [string, React.ReactNode]] : []),
            [t('doc.balance'), <Money key="b" value={inv.balance} bold testID="invoice-balance" />],
          ]}
        />
        <Button mode="contained" icon="share-variant" onPress={share} loading={sharing} testID="invoice-share">{t('mobile.share')}</Button>
        {inv.status === 'Issued' && inv.balance > 0 && (
          <Button mode="outlined" icon="cash" onPress={() => { setAmount(inv.balance.toFixed(2)); setPaying(true); }}>{t('invoice.addPayment')}</Button>
        )}
        {inv.status === 'Issued' && (
          <Button mode="outlined" icon="undo-variant" onPress={() => router.push({ pathname: '/credit-note/new', params: { invoiceId: String(id) } })}>{t('invoice.creditNote')}</Button>
        )}
        {inv.payments.length > 0 && (
          <Card>
            <Card.Title title={t('invoice.payments')} />
            {inv.payments.map((p) => (
              <List.Item key={p.id} title={t(`invoice.${p.method}`)} description={formatDate(p.date)} right={() => <View style={{ justifyContent: 'center' }}><Money value={p.amount} /></View>} />
            ))}
          </Card>
        )}
        {inv.creditNotes.length > 0 && (
          <Card>
            <Card.Title title={t('nav.creditNotes')} />
            {inv.creditNotes.map((c) => (
              <List.Item key={c.id} title={<Ltr>{c.number}</Ltr>} description={c.reason} onPress={() => router.push(`/credit-note/${c.id}`)} right={() => <View style={{ justifyContent: 'center' }}><Money value={c.total} /></View>} />
            ))}
          </Card>
        )}
        <Portal>
          <Dialog visible={paying} onDismiss={() => setPaying(false)}>
            <Dialog.Title>{t('invoice.addPayment')}</Dialog.Title>
            <Dialog.Content style={{ gap: 8 }}>
              <TextInput label={t('invoice.amount')} value={amount} onChangeText={setAmount} keyboardType="decimal-pad" mode="outlined" />
              <SegmentedButtons density="small" value={method} onValueChange={(v) => setMethod(v as Schemas['PaymentMethod'])} buttons={METHODS.map((m) => ({ value: m, label: t(`invoice.${m}`) }))} />
              <ErrorText error={error} />
              <Text style={{ opacity: 0.6 }}>{t('doc.balance')}: {inv.balance.toFixed(2)}</Text>
            </Dialog.Content>
            <Dialog.Actions>
              <Button onPress={() => setPaying(false)}>{t('common.cancel')}</Button>
              <Button onPress={pay}>{t('common.save')}</Button>
            </Dialog.Actions>
          </Dialog>
        </Portal>
      </Screen>
    </Guard>
  );
}
