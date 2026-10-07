import { useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert } from 'react-native';
import { Button, SegmentedButtons, Text } from 'react-native-paper';
import { Guard } from '../../components/Guard';
import {
  ClientField,
  LinesEditor,
  newLine,
  noDiscount,
  preview,
  toDiscountRequest,
  toRequests,
  type EditableDiscount,
  type EditableLine,
} from '../../components/DocumentEditor';
import { ErrorText, Screen } from '../../components/ui';
import { $api, fetchClient, type Schemas } from '../../lib/api';

export const METHODS: Schemas['PaymentMethod'][] = ['Cash', 'Card', 'BankTransfer', 'Cheque'];

export default function NewInvoiceScreen() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const settings = $api.useQuery('get', '/api/settings');
  const [client, setClient] = useState<{ id: number; name: string } | null>(null);
  const [lines, setLines] = useState<EditableLine[]>([newLine()]);
  const [discount, setDiscount] = useState<EditableDiscount>(noDiscount);
  const [paid, setPaid] = useState<'full' | 'none'>('full');
  const [method, setMethod] = useState<Schemas['PaymentMethod']>('Cash');
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);
  const vatRate = settings.data?.vatRate ?? 0.05;
  const total = preview(lines, vatRate, discount).total;

  const issue = async () => {
    if (!client) return;
    setBusy(true);
    setError(null);
    const { data, error: e } = await fetchClient.POST('/api/invoices', {
      body: {
        clientId: client.id,
        date: null,
        lines: toRequests(lines),
        discount: toDiscountRequest(discount),
        terms: null,
        payment: paid === 'full' && total.gt(0) ? { amount: Number(total.toFixed(2)), method, date: null, reference: null } : null,
      },
    });
    setBusy(false);
    if (e || !data) return setError(e);
    if (data.warnings.length) Alert.alert(t('item.lowStock'), data.warnings.map((w) => t('doc.stockWarning', { name: w.itemName, qty: w.stockAfter })).join('\n'));
    await queryClient.invalidateQueries();
    router.replace(`/invoice/${data.invoice.id}`);
  };

  return (
    <Guard>
      <Screen>
        {settings.data && !settings.data.isComplete && <Text style={{ color: '#c62828' }}>{t('doc.settingsIncomplete')}</Text>}
        <ClientField client={client} onChange={setClient} />
        <LinesEditor lines={lines} onChange={setLines} vatRate={vatRate} discount={discount} onDiscountChange={setDiscount} />
        <Text variant="titleSmall">{t('invoice.paymentOnIssue')}</Text>
        <SegmentedButtons value={paid} onValueChange={(v) => setPaid(v as 'full' | 'none')} buttons={[{ value: 'full', label: t('invoice.paidInFull') }, { value: 'none', label: t('invoice.notPaid') }]} />
        {paid === 'full' && (
          <SegmentedButtons density="small" value={method} onValueChange={(v) => setMethod(v as Schemas['PaymentMethod'])} buttons={METHODS.map((m) => ({ value: m, label: t(`invoice.${m}`) }))} />
        )}
        <ErrorText error={error} />
        <Button mode="contained" onPress={issue} loading={busy} disabled={busy || !client} testID="document-submit">{t('invoice.issue')}</Button>
      </Screen>
    </Guard>
  );
}
