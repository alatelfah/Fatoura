import { useTranslation } from 'react-i18next';
import { View } from 'react-native';
import { Card, Divider, Text } from 'react-native-paper';
import type { Schemas } from '../lib/api';
import { formatDate, formatMoney, formatQty } from '../lib/format';
import { Ltr, Money, Row } from './ui';

type Line = { id: number; lineNo: number; description: string; quantity: number; unitPrice: number; discount?: number; vat: number; total: number };

/** Read-only document summary: header, client, lines and totals. */
export function DocumentDetails({ number, date, client, lines, discount = 0, subTotal, vatTotal, total, currency, extra }: {
  number: string;
  date: string;
  client: Schemas['PartyDto'];
  lines: Line[];
  /** Document discount in money; sub total is shown before it. */
  discount?: number;
  subTotal: number;
  vatTotal: number;
  total: number;
  /** Amounts are in this currency; another currency also shows the rate and the VAT and total in AED. */
  currency?: Schemas['DocumentCurrencyDto'];
  extra?: Array<[string, React.ReactNode]>;
}) {
  const { t } = useTranslation();
  const foreign = currency && currency.code !== 'AED' ? currency : null;
  return (
    <>
      <Card>
        <Card.Content>
          <Row label={t('doc.number')}><Ltr bold>{number}</Ltr></Row>
          <Row label={t('doc.date')}>{formatDate(date)}</Row>
          {foreign && <Row label={t('doc.currency')}><Ltr>{t('doc.rateInfo', { currency: foreign.code, rate: foreign.exchangeRate })}</Ltr></Row>}
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
              <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
                <View style={{ flex: 1, flexDirection: 'row', flexWrap: 'wrap', columnGap: 10 }}>
                  <Ltr style={{ opacity: 0.7 }}>{formatQty(l.quantity)} × {formatMoney(l.unitPrice)}</Ltr>
                  {!!l.discount && (
                    <View style={{ flexDirection: 'row', gap: 4 }}>
                      <Text style={{ opacity: 0.7 }}>{t('doc.discount')}</Text>
                      <Ltr style={{ opacity: 0.7 }}>-{formatMoney(l.discount)}</Ltr>
                    </View>
                  )}
                  <View style={{ flexDirection: 'row', gap: 4 }}>
                    <Text style={{ opacity: 0.7 }}>{t('doc.vat')}</Text>
                    <Ltr style={{ opacity: 0.7 }}>{formatMoney(l.vat)}</Ltr>
                  </View>
                </View>
                <Money value={l.total} bold />
              </View>
            </View>
          ))}
          <Divider />
          <Row label={t('doc.subTotal')}><Money value={subTotal + discount} currency={foreign?.code} /></Row>
          {discount !== 0 && (
            <>
              <Row label={t('doc.discount')}><Money value={-discount} testID="doc-discount" /></Row>
              <Row label={t('doc.totalExclVat')}><Money value={subTotal} /></Row>
            </>
          )}
          <Row label={t('doc.vatTotal')}><Money value={vatTotal} currency={foreign?.code} /></Row>
          <Row label={foreign ? t('doc.totalIn', { currency: foreign.code }) : t('doc.total')}><Money value={total} bold testID="doc-total" /></Row>
          {foreign && (
            <>
              <Row label={t('doc.vatAed')}><Money value={foreign.vatTotalAed} /></Row>
              <Row label={t('doc.totalAed')}><Money value={foreign.totalAed} /></Row>
            </>
          )}
        </Card.Content>
      </Card>
    </>
  );
}
