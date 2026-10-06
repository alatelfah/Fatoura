import { useQueryClient } from '@tanstack/react-query';
import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert } from 'react-native';
import { Button } from 'react-native-paper';
import { DocumentDetails } from '../../components/DocumentDetails';
import { Guard } from '../../components/Guard';
import { ErrorText, Loading, Ltr, Screen, StatusChip } from '../../components/ui';
import { $api, fetchClient, sharePdf } from '../../lib/api';
import { formatDate } from '../../lib/format';
import { QUOTATION_COLORS } from '../(tabs)/quotations';

export default function QuotationScreen() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const id = Number(useLocalSearchParams<{ id: string }>().id);
  const { data: q, refetch, isRefetching } = $api.useQuery('get', '/api/quotations/{id}', { params: { path: { id } } });
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);
  if (!q) return <Guard><Loading /></Guard>;

  const convert = async () => {
    setBusy(true);
    const { data, error: e } = await fetchClient.POST('/api/quotations/{id}/convert', { params: { path: { id } }, body: { payment: null } });
    setBusy(false);
    if (e || !data) return setError(e);
    await queryClient.invalidateQueries();
    router.replace(`/invoice/${data.invoice.id}`);
  };

  return (
    <Guard>
      <Screen refreshing={isRefetching} onRefresh={refetch}>
        <StatusChip label={t(`quotation.${q.status}`)} color={QUOTATION_COLORS[q.status]} />
        <DocumentDetails
          number={q.number}
          date={q.date}
          client={q.client}
          lines={q.lines}
          subTotal={q.subTotal}
          vatTotal={q.vatTotal}
          total={q.total}
          extra={[
            [t('doc.validUntil'), formatDate(q.validUntil)],
            ...(q.convertedInvoiceNumber ? [[t('quotation.invoice'), <Ltr key="i">{q.convertedInvoiceNumber}</Ltr>] as [string, React.ReactNode]] : []),
          ]}
        />
        <Button mode="outlined" icon="share-variant" onPress={() => sharePdf(`/api/quotations/${id}/pdf`, `Quotation-${q.number.replace(/[^\w-]/g, '-')}.pdf`)}>{t('mobile.share')}</Button>
        {q.status !== 'Converted' && q.status !== 'Rejected' && (
          <Button mode="contained" icon="swap-horizontal" loading={busy} onPress={() => Alert.alert(t('quotation.convert'), t('quotation.convertConfirm'), [
            { text: t('common.cancel'), style: 'cancel' },
            { text: t('common.confirm'), onPress: convert },
          ])}>
            {t('quotation.convert')}
          </Button>
        )}
        <ErrorText error={error} />
      </Screen>
    </Guard>
  );
}
