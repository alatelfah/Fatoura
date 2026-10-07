import { Col, Form, InputNumber, Select, type FormInstance } from 'antd';
import { useTranslation } from 'react-i18next';
import { BASE_CURRENCY } from '@fatoura/shared';
import { $api, type Schemas } from '../api/client';

export interface CurrencyFormValue {
  currency?: string;
  exchangeRate?: number | null;
}

/**
 * Currency and exchange rate (AED per unit) for a document form, in the form's `currency` and `exchangeRate` fields.
 * Picking a currency fills in its current rate from Settings; the rate can then be changed. AED has no rate.
 */
export function CurrencyFields({ form, existing }: { form: FormInstance; existing?: string }) {
  const { t } = useTranslation();
  const { data } = $api.useQuery('get', '/api/settings/currencies');
  const currency = (Form.useWatch('currency', form) as string | undefined) ?? BASE_CURRENCY;
  const options = [BASE_CURRENCY, ...(data ?? []).filter((c) => c.isActive || c.code === existing).map((c) => c.code)];

  const pick = (code: string) => {
    const rate = (data ?? []).find((c) => c.code === code)?.rateToAed;
    form.setFieldValue('exchangeRate', code === BASE_CURRENCY ? null : (rate ?? null));
  };

  return (
    <>
      <Col xs={12} md={4}>
        <Form.Item name="currency" label={t('doc.currency')}>
          <Select
            options={options.map((code) => ({ value: code, label: code }))}
            onChange={pick}
            data-testid="document-currency"
          />
        </Form.Item>
      </Col>
      {currency !== BASE_CURRENCY && (
        <Col xs={12} md={6}>
          <Form.Item name="exchangeRate" label={t('doc.exchangeRate', { currency })} rules={[{ required: true, message: t('common.required') }]}>
            <InputNumber min={0.000001} max={100000} step={0.0001} style={{ width: '100%' }} data-testid="document-rate" />
          </Form.Item>
        </Col>
      )}
    </>
  );
}

/** Form values for a document's currency, from the API's currency of an existing document. */
export function fromCurrency(c: Schemas['DocumentCurrencyDto'] | undefined): CurrencyFormValue {
  return !c || c.code === BASE_CURRENCY ? { currency: BASE_CURRENCY, exchangeRate: null } : { currency: c.code, exchangeRate: c.exchangeRate };
}

/** The request fields for a document's currency. */
export function toCurrencyRequest(v: CurrencyFormValue): { currency: string; exchangeRate: number | null } {
  const currency = v.currency ?? BASE_CURRENCY;
  return { currency, exchangeRate: currency === BASE_CURRENCY || v.exchangeRate == null ? null : Number(v.exchangeRate) };
}
