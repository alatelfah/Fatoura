import { useTranslation } from 'react-i18next';
import { View } from 'react-native';
import { Card, Divider, Text } from 'react-native-paper';
import type { Schemas } from '../lib/api';
import { formatDate, formatQty } from '../lib/format';
import { Ltr, Money, Row } from './ui';

type Line = { id: number; lineNo: number; description: string; quantity: number; unitPrice: number; vat: number; total: number };

/** Read-only document summary: header, client, lines and totals. */
export function DocumentDetails({ number, date, client, lines, subTotal, vatTotal, total, extra }: {
  number: string; date: string; client: Schemas['PartyDto']; lines: Line[]; subTotal: number; vatTotal: number; total: number; extra?: Array<[string, React.ReactNode]>;
}) {
  const { t } = useTranslation();
  return (
    <>
      <Card>
        <Card.Content>
          <Row label={t('doc.number')}><Ltr bold>{number}</Ltr></Row>
          <Row label={t('doc.date')}>{formatDate(date)}</Row>
          {extra?.map(([label, value]) => <Row key={label} label={label}>{value}</Row>)}
          <Divider style={{ marginVertical: 8 }} />
          <Text variant="titleMedium">{client.name}</Text>
          {!!client.address && <Text style={{ opacity: 0.7 }}>{client.address}</Text>}
          {!!client.trn && <Row label={t('contact.trn')}><Ltr>{client.trn}</Ltr></Row>}
        </Card.Content>
      </Card>
      <Card>
        <Card.Content style={{ gap: 8 }}>
          {[...lines].sort((a, b) => a.lineNo - b.lineNo).map((l) => (
            <View key={l.id}>
              <Text>{l.lineNo}. {l.description}</Text>
              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <Text style={{ opacity: 0.7, writingDirection: 'ltr' }}>{formatQty(l.quantity)} × {l.unitPrice.toFixed(2)} · VAT {l.vat.toFixed(2)}</Text>
                <Money value={l.total} bold />
              </View>
            </View>
          ))}
          <Divider />
          <Row label={t('doc.subTotal')}><Money value={subTotal} /></Row>
          <Row label={t('doc.vatTotal')}><Money value={vatTotal} /></Row>
          <Row label={t('doc.total')}><Money value={total} bold testID="doc-total" /></Row>
        </Card.Content>
      </Card>
    </>
  );
}
