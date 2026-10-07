import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Flex, Form, Input, InputNumber, Select, Typography, type FormInstance } from 'antd';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { BASE_CURRENCY, previewDocument, TAX_CATEGORIES, toAed, type DiscountKind, type TaxCategory } from '@fatoura/shared';
import { $api, type Schemas } from '../api/client';
import { Money } from './Ltr';

export interface LineFormValue {
  itemId?: number | null;
  description?: string;
  expenseCategory?: string;
  quantity?: number | null;
  unitPrice?: number | null;
  taxCategory?: TaxCategory;
}

export interface DiscountFormValue {
  kind?: DiscountKind;
  value?: number | null;
}

interface Props {
  form: FormInstance;
  vatRate: number;
  /** Purchases price items at cost and allow expense lines. */
  purchase?: boolean;
  /** Sales documents take a document-level discount, edited in the form's `discount` field. */
  discount?: boolean;
}

export const emptyLine: LineFormValue = { itemId: null, description: '', quantity: 1, unitPrice: undefined, taxCategory: 'Standard' };

/** Editable document lines with live per-line VAT and totals (same math as the server, via @fatoura/shared). */
export function DocumentLinesEditor({ form, vatRate, purchase, discount }: Props) {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');
  const items = $api.useQuery('get', '/api/items', { params: { query: { search: search || undefined, pageSize: 100 } } });
  const lines = (Form.useWatch('lines', form) as LineFormValue[] | undefined) ?? [];
  const discountValue = Form.useWatch('discount', form) as DiscountFormValue | undefined;
  const currency = (Form.useWatch('currency', form) as string | undefined) ?? BASE_CURRENCY;
  const exchangeRate = Form.useWatch('exchangeRate', form) as number | null | undefined;
  const totals = previewLines(lines, vatRate, discount ? discountValue : undefined);
  const aed = currency !== BASE_CURRENCY && exchangeRate ? toAed(totals, exchangeRate) : null;
  const byId = new Map((items.data?.items ?? []).map((i) => [i.id, i] as const));

  const pickItem = (index: number, item: Schemas['ItemDto'] | undefined) => {
    if (!item) return;
    const current = form.getFieldValue(['lines', index]) as LineFormValue;
    form.setFieldValue(['lines', index], {
      ...current,
      itemId: item.id,
      description: item.description || item.name,
      unitPrice: purchase ? (item.avgCost > 0 ? Number(item.avgCost.toFixed(2)) : current.unitPrice) : item.unitPrice,
      taxCategory: item.taxCategory,
    });
  };

  return (
    <div className="lines-editor">
      <div className="line-grid line-head">
        <span>#</span>
        <span>{t('doc.description')}</span>
        <span>{t('doc.qty')}</span>
        <span>{purchase ? t('purchase.unitCost') : t('doc.unitPriceIn', { currency })}</span>
        <span>{t('item.taxCategory')}</span>
        <span className="num">{t('doc.vat')}</span>
        <span className="num">{t('doc.amountIn', { currency })}</span>
        <span />
      </div>
      <Form.List
        name="lines"
        rules={[
          {
            validator: async (_, value: unknown[] | undefined) => {
              if (!value || value.length === 0) throw new Error(t('doc.noLines'));
            },
          },
        ]}
      >
        {(fields, { add, remove }, { errors }) => (
          <>
            {fields.map((field, index) => (
              <div key={field.key} className="line-grid line-row" data-testid={`line-${index}`}>
                <span className="line-no">{index + 1}</span>
                <div>
                  <Form.Item name={[field.name, 'itemId']} noStyle>
                    <Select
                      allowClear
                      showSearch={{ filterOption: false, onSearch: setSearch }}
                      placeholder={t('doc.pickItem')}
                      options={(items.data?.items ?? []).map((i) => ({ value: i.id, label: i.name }))}
                      onChange={(id: number | undefined) => pickItem(index, id ? byId.get(id) : undefined)}
                      style={{ width: '100%', marginBottom: 4 }}
                      size="small"
                      data-testid={`line-${index}-item`}
                    />
                  </Form.Item>
                  <Form.Item name={[field.name, 'description']} rules={[{ required: true, whitespace: true, message: t('common.required') }]} style={{ marginBottom: 0 }}>
                    <Input.TextArea autoSize={{ minRows: 1, maxRows: 4 }} maxLength={1000} placeholder={t('doc.description')} data-testid={`line-${index}-description`} />
                  </Form.Item>
                  {purchase && !lines[index]?.itemId && (
                    <Form.Item name={[field.name, 'expenseCategory']} style={{ marginBottom: 0, marginTop: 4 }}>
                      <Input size="small" maxLength={100} placeholder={t('purchase.expenseCategory')} />
                    </Form.Item>
                  )}
                </div>
                <Form.Item name={[field.name, 'quantity']} rules={[{ required: true, message: t('common.required') }, { validator: async (_, v?: number) => { if (v != null && Math.round(v * 1000) !== v * 1000) throw new Error('0.001'); } }]} style={{ marginBottom: 0 }}>
                  <InputNumber min={0.001} max={1_000_000} step={1} style={{ width: '100%' }} data-testid={`line-${index}-qty`} />
                </Form.Item>
                <Form.Item name={[field.name, 'unitPrice']} rules={[{ required: true, message: t('common.required') }]} style={{ marginBottom: 0 }}>
                  <InputNumber min={0} max={999_999_999} precision={2} style={{ width: '100%' }} data-testid={`line-${index}-price`} />
                </Form.Item>
                <Form.Item name={[field.name, 'taxCategory']} style={{ marginBottom: 0 }}>
                  <Select options={TAX_CATEGORIES.map((c) => ({ value: c, label: t(`item.${c}`) }))} />
                </Form.Item>
                <span className="num">
                  <Money value={totals.lines[index]?.vat.toFixed(2)} />
                </span>
                <span className="num" data-testid={`line-${index}-amount`}>
                  <Money value={totals.lines[index]?.total.toFixed(2)} />
                  {totals.lines[index]?.discount.gt(0) && (
                    <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                      {t('doc.discountShare', { amount: totals.lines[index]!.discount.toFixed(2) })}
                    </Typography.Text>
                  )}
                </span>
                <Button type="text" danger icon={<DeleteOutlined />} onClick={() => remove(field.name)} aria-label={t('common.delete')} />
              </div>
            ))}
            <Form.ErrorList errors={errors} />
            <Button type="dashed" icon={<PlusOutlined />} onClick={() => add({ ...emptyLine })} style={{ marginTop: 8 }} data-testid="add-line">
              {t('doc.addLine')}
            </Button>
          </>
        )}
      </Form.List>

      <Flex vertical align="end" gap={4} className="totals" data-testid="totals">
        <Totals label={t('doc.subTotal')} value={totals.grossSubTotal.toFixed(2)} />
        {discount && (
          <Flex justify="end" align="start" gap={4} style={{ minWidth: 260 }}>
            <Form.Item name={['discount', 'kind']} noStyle>
              <Select
                style={{ width: 150 }}
                aria-label={t('doc.discount')}
                data-testid="discount-kind"
                options={[
                  { value: 'None', label: t('doc.discountNone') },
                  { value: 'Amount', label: t('doc.discountAmount') },
                  { value: 'Percent', label: t('doc.discountPercent') },
                ]}
                onChange={(kind: DiscountKind) => kind === 'None' && form.setFieldValue(['discount', 'value'], null)}
              />
            </Form.Item>
            <Form.Item
              name={['discount', 'value']}
              style={{ marginBottom: 0 }}
              dependencies={[['discount', 'kind']]}
              rules={[
                {
                  validator: async (_, v?: number | null) => {
                    if (discountValue?.kind === 'Percent' && v != null && v > 100) throw new Error(`${t('doc.discount')} ≤ 100%`);
                    if (discountValue?.kind === 'Amount' && v != null && totals.grossSubTotal.lt(v)) throw new Error(`${t('doc.discount')} ≤ ${totals.grossSubTotal.toFixed(2)}`);
                  },
                },
              ]}
            >
              <InputNumber
                min={0}
                max={discountValue?.kind === 'Percent' ? 100 : undefined}
                precision={2}
                style={{ width: 110 }}
                aria-label={t('doc.discount')}
                disabled={!discountValue?.kind || discountValue.kind === 'None'}
                data-testid="discount-value"
              />
            </Form.Item>
          </Flex>
        )}
        {totals.discount.gt(0) && (
          <>
            <Totals label={t('doc.discount')} value={totals.discount.times(-1).toFixed(2)} testId="discount-amount" />
            <Totals label={t('doc.totalExclVat')} value={totals.subTotal.toFixed(2)} />
          </>
        )}
        <Totals label={`${t('doc.vatTotal')} ${Number((vatRate * 100).toFixed(2))}%`} value={totals.vatTotal.toFixed(2)} />
        <Totals label={currency === BASE_CURRENCY ? t('doc.total') : t('doc.totalIn', { currency })} value={totals.total.toFixed(2)} strong testId="grand-total" />
        {aed && (
          <>
            <Totals label={t('doc.vatAed')} value={aed.vatTotal.toFixed(2)} testId="vat-aed" />
            <Totals label={t('doc.totalAed')} value={aed.total.toFixed(2)} testId="total-aed" />
          </>
        )}
      </Flex>
    </div>
  );
}

function Totals({ label, value, strong, testId }: { label: string; value: string; strong?: boolean; testId?: string }) {
  return (
    <Flex gap={24} justify="space-between" style={{ minWidth: 260 }}>
      <Typography.Text strong={strong}>{label}</Typography.Text>
      <span data-testid={testId}>
        <Money value={value} strong={strong} />
      </span>
    </Flex>
  );
}

/** Live totals for form lines, with the document discount spread over them (invalid input is skipped). */
export function previewLines(lines: LineFormValue[] | undefined, vatRate: number, discount?: DiscountFormValue) {
  return previewDocument(
    (lines ?? []).map((l) => ({ qty: l?.quantity ?? null, unitPrice: l?.unitPrice ?? null, tax: l?.taxCategory ?? 'Standard' })),
    vatRate,
    toDiscountRequest(discount),
  );
}

/** The API's discount request, or null for no discount. */
export function toDiscountRequest(d: DiscountFormValue | undefined): Schemas['DocumentDiscountRequest'] | null {
  if (!d?.kind || d.kind === 'None' || !d.value) return null;
  return { kind: d.kind, value: Number(d.value) };
}

export function fromDiscount(d: Schemas['DocumentDiscountDto'] | undefined): DiscountFormValue {
  return !d || d.kind === 'None' ? { kind: 'None', value: null } : { kind: d.kind, value: d.value };
}

/** Converts form lines to the API's line request shape. */
export function toLineRequests(lines: LineFormValue[] | undefined): Schemas['DocumentLineRequest'][] {
  return (lines ?? []).map((l) => ({
    itemId: l.itemId ?? null,
    description: (l.description ?? '').trim(),
    quantity: Number(l.quantity ?? 0),
    unitPrice: Number(l.unitPrice ?? 0),
    taxCategory: l.taxCategory ?? 'Standard',
  }));
}

export function fromLines(lines: Array<Schemas['DocumentLineDto'] | Schemas['InvoiceLineDto']>): LineFormValue[] {
  return [...lines]
    .sort((a, b) => a.lineNo - b.lineNo)
    .map((l) => ({ itemId: l.itemId ?? null, description: l.description, quantity: l.quantity, unitPrice: l.unitPrice, taxCategory: l.taxCategory }));
}
