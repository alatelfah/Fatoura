import { Card, Col, Descriptions, Row, Table, Typography } from 'antd';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { Schemas } from '../api/client';
import { Ltr, Money } from './Ltr';
import { formatDate, formatQty } from '../utils/format';

/** Purchase lines have no discount. */
type Line = (Schemas['DocumentLineDto'] | Schemas['InvoiceLineDto'] | Schemas['CreditNoteLineDto'] | Schemas['PurchaseLineDto']) & { discount?: number };

interface Props {
  number: string;
  date: string;
  client: Schemas['PartyDto'];
  info?: Array<{ label: string; value: ReactNode }>;
  lines: Line[];
  /** Document discount in money; with one, sub total is shown before it and a Discount column is added. */
  discount?: number;
  /** How the discount was entered, e.g. "10%". */
  discountLabel?: string;
  subTotal: number;
  vatTotal: number;
  total: number;
  terms?: Schemas['TermsDto'];
  extraColumns?: Array<{ title: string; render: (line: Line) => ReactNode }>;
}

/** Read-only view of a sales document, laid out like the printed version. */
export function DocumentView({ number, date, client, info = [], lines, discount = 0, discountLabel, subTotal, vatTotal, total, terms, extraColumns = [] }: Props) {
  const { t } = useTranslation();
  const hasDiscount = discount !== 0;
  return (
    <Card>
      <Row gutter={[24, 16]}>
        <Col xs={24} md={14}>
          <Descriptions column={1} size="small" items={[
            { key: 'client', label: t('contact.clientName'), children: <strong>{client.name}</strong> },
            { key: 'address', label: t('contact.address'), children: client.address || '—' },
            { key: 'phone', label: t('contact.phone'), children: <Ltr>{client.phone || '—'}</Ltr> },
            { key: 'trn', label: t('contact.trn'), children: <strong><Ltr>{client.trn || '—'}</Ltr></strong> },
          ]} />
        </Col>
        <Col xs={24} md={10}>
          <Descriptions column={1} size="small" items={[
            { key: 'number', label: t('doc.number'), children: <strong data-testid="doc-number"><Ltr>{number}</Ltr></strong> },
            { key: 'date', label: t('doc.date'), children: formatDate(date) },
            ...info.map((i, idx) => ({ key: `info-${idx}`, label: i.label, children: i.value })),
          ]} />
        </Col>
      </Row>
      <Table
        style={{ marginTop: 16 }}
        size="small"
        rowKey="id"
        pagination={false}
        dataSource={[...lines].sort((a, b) => a.lineNo - b.lineNo)}
        scroll={{ x: 800 }}
        columns={[
          { title: 'Sl.No', dataIndex: 'lineNo', width: 60, align: 'center' },
          { title: t('doc.description'), dataIndex: 'description', render: (v: string) => <span style={{ whiteSpace: 'pre-wrap' }}>{v}</span> },
          { title: t('doc.qty'), dataIndex: 'quantity', className: 'num', render: (v: number) => <Ltr>{formatQty(v)}</Ltr> },
          { title: t('doc.unitPrice'), dataIndex: 'unitPrice', className: 'num', render: (v: number) => <Money value={v} /> },
          ...(hasDiscount ? [{ title: t('doc.discount'), dataIndex: 'discount', className: 'num', render: (v: number) => <Money value={v} /> }] : []),
          { title: t('doc.vat'), dataIndex: 'vat', className: 'num', render: (v: number, l: Line) => <span title={t(`item.${l.taxCategory}`)}><Money value={v} /></span> },
          { title: t('doc.amount'), dataIndex: 'total', className: 'num', render: (v: number) => <Money value={v} /> },
          ...extraColumns.map((c, i) => ({ key: `extra-${i}`, title: c.title, className: 'num', render: (_: unknown, l: Line) => c.render(l) })),
        ]}
      />
      <Row justify="end" style={{ marginTop: 16 }}>
        <Col xs={24} sm={12} md={8}>
          <Descriptions column={1} size="small" bordered items={[
            { key: 'sub', label: t('doc.subTotal'), children: <Money value={subTotal + discount} /> },
            ...(hasDiscount
              ? [
                  {
                    key: 'discount',
                    label: discountLabel ? `${t('doc.discount')} (${discountLabel})` : t('doc.discount'),
                    children: <span data-testid="doc-discount"><Money value={-discount} /></span>,
                  },
                  { key: 'net', label: t('doc.totalExclVat'), children: <Money value={subTotal} /> },
                ]
              : []),
            { key: 'vat', label: t('doc.vatTotal'), children: <Money value={vatTotal} /> },
            { key: 'total', label: <strong>{t('doc.total')}</strong>, children: <span data-testid="doc-total"><Money value={total} strong /></span> },
          ]} />
        </Col>
      </Row>
      {terms && (terms.paymentTerms || terms.completionOfWork || terms.notes) && (
        <div style={{ marginTop: 16 }}>
          <Typography.Text strong>{t('doc.terms')}</Typography.Text>
          {terms.paymentTerms && <Typography.Paragraph style={{ marginBottom: 4 }}>{t('doc.paymentTerms')}: {terms.paymentTerms}</Typography.Paragraph>}
          {terms.completionOfWork && <Typography.Paragraph style={{ marginBottom: 4 }}>{t('doc.completionOfWork')}: {terms.completionOfWork}</Typography.Paragraph>}
          {terms.notes && <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 4 }}>{terms.notes}</Typography.Paragraph>}
        </div>
      )}
    </Card>
  );
}
