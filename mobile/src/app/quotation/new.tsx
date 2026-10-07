import { useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from 'react-native-paper';
import { Guard } from '../../components/Guard';
import {
  aedCurrency,
  ClientField,
  CurrencyField,
  LinesEditor,
  newLine,
  noDiscount,
  toCurrencyRequest,
  toDiscountRequest,
  toRequests,
  type EditableCurrency,
  type EditableDiscount,
  type EditableLine,
} from '../../components/DocumentEditor';
import { ErrorText, Screen } from '../../components/ui';
import { $api, fetchClient } from '../../lib/api';

export default function NewQuotationScreen() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const settings = $api.useQuery('get', '/api/settings');
  const [client, setClient] = useState<{ id: number; name: string } | null>(null);
  const [lines, setLines] = useState<EditableLine[]>([newLine()]);
  const [discount, setDiscount] = useState<EditableDiscount>(noDiscount);
  const [currency, setCurrency] = useState<EditableCurrency>(aedCurrency);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const save = async () => {
    if (!client) return;
    setBusy(true);
    const { data, error: e } = await fetchClient.POST('/api/quotations', { body: { clientId: client.id, date: null, validUntil: null, lines: toRequests(lines), discount: toDiscountRequest(discount), ...toCurrencyRequest(currency), terms: null } });
    setBusy(false);
    if (e || !data) return setError(e);
    await queryClient.invalidateQueries({ queryKey: ['get', '/api/quotations'] });
    router.replace(`/quotation/${data.id}`);
  };

  return (
    <Guard>
      <Screen>
        <ClientField client={client} onChange={setClient} />
        <CurrencyField value={currency} onChange={setCurrency} />
        <LinesEditor lines={lines} onChange={setLines} vatRate={settings.data?.vatRate ?? 0.05} discount={discount} onDiscountChange={setDiscount} currency={currency} />
        <ErrorText error={error} />
        <Button mode="contained" onPress={save} loading={busy} disabled={busy || !client}>{t('common.save')}</Button>
      </Screen>
    </Guard>
  );
}
