import { useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { Button } from 'react-native-paper';
import { DocumentDetails } from '../../components/DocumentDetails';
import { Guard } from '../../components/Guard';
import { Loading, Ltr, Screen } from '../../components/ui';
import { $api, sharePdf } from '../../lib/api';

export default function CreditNoteScreen() {
  const { t } = useTranslation();
  const id = Number(useLocalSearchParams<{ id: string }>().id);
  const { data: c } = $api.useQuery('get', '/api/credit-notes/{id}', { params: { path: { id } } });
  if (!c) return <Guard><Loading /></Guard>;
  return (
    <Guard>
      <Screen>
        <DocumentDetails
          number={c.number}
          date={c.date}
          client={c.client}
          lines={c.lines}
          subTotal={c.subTotal}
          vatTotal={c.vatTotal}
          total={c.total}
          extra={[[t('creditNote.againstInvoice'), <Ltr key="i">{c.invoiceNumber}</Ltr>], [t('creditNote.reason'), c.reason]]}
        />
        <Button mode="contained" icon="share-variant" onPress={() => sharePdf(`/api/credit-notes/${id}/pdf`, `CreditNote-${c.number.replace(/[^\w-]/g, '-')}.pdf`)}>{t('mobile.share')}</Button>
      </Screen>
    </Guard>
  );
}
