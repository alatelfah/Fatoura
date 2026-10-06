import { App, Alert, Button, Card, Col, Collapse, DatePicker, Form, Input, InputNumber, Radio, Row, Select, Skeleton, Typography } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { previewDocument } from '@fatoura/shared';
import { $api, fetchClient, type Schemas } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { ContactSelect } from '../../components/ClientSelect';
import { DocumentLinesEditor, emptyLine, fromLines, toLineRequests, type LineFormValue } from '../../components/DocumentLinesEditor';
import { PageHeader } from '../../components/PageHeader';
import { applyProblem } from '../../components/problems';
import { isoDate } from '../../utils/format';

type Mode = 'quotation' | 'invoice';

interface FormValues {
  clientId: number;
  date?: Dayjs;
  validUntil?: Dayjs;
  lines: LineFormValue[];
  paymentTerms?: string;
  completionOfWork?: string;
  notes?: string;
  closingText?: string;
  paymentMode?: 'full' | 'none' | 'partial';
  paymentMethod?: Schemas['PaymentMethod'];
  paymentAmount?: number;
  paymentReference?: string;
}

interface Props {
  mode: Mode;
  /** Present when editing an existing quotation or (Admin) invoice. */
  existing?: Schemas['QuotationDto'] | Schemas['InvoiceDto'];
}

export const PAYMENT_METHODS: Schemas['PaymentMethod'][] = ['Cash', 'Card', 'BankTransfer', 'Cheque'];

/** Shared create/edit form for quotations and tax invoices (same header, lines and terms). */
export function SalesDocumentForm({ mode, existing }: Props) {
  const { t } = useTranslation();
  const { message, notification } = App.useApp();
  const { isAdmin } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<FormValues>();
  const [saving, setSaving] = useState(false);
  const settings = $api.useQuery('get', '/api/settings');
  const vatRate = settings.data?.vatRate ?? 0.05;
  const lines = Form.useWatch('lines', form);
  const paymentMode = Form.useWatch('paymentMode', form);
  const total = previewDocument((lines ?? []).map((l) => ({ qty: l?.quantity ?? null, unitPrice: l?.unitPrice ?? null, tax: l?.taxCategory ?? 'Standard' })), vatRate).total;

  useEffect(() => {
    if (!settings.data) return;
    if (existing) {
      form.setFieldsValue({
        clientId: existing.clientId,
        date: dayjs(existing.date),
        validUntil: 'validUntil' in existing ? dayjs(existing.validUntil) : undefined,
        lines: fromLines(existing.lines),
        ...existing.terms,
      });
    } else {
      form.setFieldsValue({
        lines: [{ ...emptyLine }],
        paymentTerms: settings.data.paymentTerms,
        completionOfWork: settings.data.completionOfWork,
        notes: settings.data.notes,
        closingText: settings.data.closingText,
        paymentMode: 'full',
        paymentMethod: 'Cash',
      });
    }
  }, [settings.data, existing, form]);

  if (settings.isLoading) return <Skeleton active />;

  const submit = async (values: FormValues) => {
    setSaving(true);
    try {
      const terms = { paymentTerms: values.paymentTerms ?? '', completionOfWork: values.completionOfWork ?? '', notes: values.notes ?? '', closingText: values.closingText ?? '' };
      const linesBody = toLineRequests(values.lines);
      if (mode === 'quotation') {
        const body = { clientId: values.clientId, date: isoDate(values.date) ?? null, validUntil: isoDate(values.validUntil) ?? null, lines: linesBody, terms };
        const { data, error } = existing
          ? await fetchClient.PUT('/api/quotations/{id}', { params: { path: { id: existing.id } }, body })
          : await fetchClient.POST('/api/quotations', { body });
        if (error) return void message.error(applyProblem(error, form));
        message.success(t('common.saved'));
        await queryClient.invalidateQueries({ queryKey: ['get', '/api/quotations'] });
        navigate(`/quotations/${data!.id}`);
        return;
      }

      const payment =
        values.paymentMode === 'none' || existing
          ? null
          : {
              amount: values.paymentMode === 'full' ? Number(total.toFixed(2)) : Number(values.paymentAmount ?? 0),
              method: values.paymentMethod ?? 'Cash',
              date: null,
              reference: values.paymentReference ?? null,
            };
      let issued: Schemas['InvoiceResult'] | undefined;
      let failure: unknown;
      if (existing) {
        const r = await fetchClient.PUT('/api/invoices/{id}', { params: { path: { id: existing.id } }, body: { clientId: values.clientId, date: isoDate(values.date)!, lines: linesBody, terms } });
        issued = r.data;
        failure = r.error;
      } else {
        const r = await fetchClient.POST('/api/invoices', { body: { clientId: values.clientId, date: isAdmin ? (isoDate(values.date) ?? null) : null, lines: linesBody, terms, payment: payment && payment.amount > 0 ? payment : null } });
        issued = r.data;
        failure = r.error;
      }
      if (!issued) return void message.error(applyProblem(failure, form));
      for (const w of issued.warnings) {
        notification.warning({ title: t('item.lowStock'), description: t('doc.stockWarning', { name: w.itemName, qty: w.stockAfter }) });
      }
      message.success(t('common.saved'));
      await queryClient.invalidateQueries();
      navigate(`/invoices/${issued.invoice.id}`);
    } finally {
      setSaving(false);
    }
  };

  const title = mode === 'quotation' ? (existing ? t('quotation.edit') : t('quotation.new')) : existing ? t('invoice.edit') : t('invoice.new');
  return (
    <>
      <PageHeader title={title} subtitle={existing ? existing.number : undefined} />
      {settings.data && !settings.data.isComplete && (
        <Alert type="warning" showIcon style={{ marginBottom: 16 }} title={t('doc.settingsIncomplete')} action={isAdmin ? <Link to="/settings">{t('nav.settings')}</Link> : undefined} />
      )}
      <Form form={form} layout="vertical" onFinish={submit} scrollToFirstError>
        <Card style={{ marginBottom: 16 }}>
          <Row gutter={16}>
            <Col xs={24} md={12}>
              <Form.Item name="clientId" label={t('doc.client')} rules={[{ required: true, message: t('common.required') }]}>
                <ContactSelect placeholder={t('doc.client')} initialLabel={existing?.client.name} />
              </Form.Item>
            </Col>
            <Col xs={12} md={6}>
              <Form.Item name="date" label={t('doc.date')} extra={mode === 'invoice' && !isAdmin ? t('invoice.cashierDateNote') : undefined}>
                <DatePicker style={{ width: '100%' }} placeholder={t('doc.today')} disabled={mode === 'invoice' && !isAdmin} disabledDate={(d) => mode === 'invoice' && d.isAfter(dayjs(), 'day')} data-testid="document-date" />
              </Form.Item>
            </Col>
            {mode === 'quotation' && (
              <Col xs={12} md={6}>
                <Form.Item name="validUntil" label={t('doc.validUntil')}>
                  <DatePicker style={{ width: '100%' }} />
                </Form.Item>
              </Col>
            )}
          </Row>
        </Card>
        <Card title={t('doc.lines')} style={{ marginBottom: 16 }}>
          <DocumentLinesEditor form={form} vatRate={vatRate} />
        </Card>
        <Collapse
          style={{ marginBottom: 16, background: '#fff' }}
          items={[
            {
              key: 'terms',
              label: t('doc.terms'),
              children: (
                <>
                  <Form.Item name="paymentTerms" label={t('doc.paymentTerms')}><Input.TextArea autoSize maxLength={1000} /></Form.Item>
                  <Form.Item name="completionOfWork" label={t('doc.completionOfWork')}><Input maxLength={1000} /></Form.Item>
                  <Form.Item name="notes" label={t('doc.notes')}><Input.TextArea autoSize={{ minRows: 3 }} maxLength={4000} /></Form.Item>
                  <Form.Item name="closingText" label={t('doc.closingText')}><Input.TextArea autoSize maxLength={1000} /></Form.Item>
                </>
              ),
            },
          ]}
        />
        {mode === 'invoice' && !existing && (
          <Card title={t('invoice.paymentOnIssue')} style={{ marginBottom: 16 }}>
            <Form.Item name="paymentMode">
              <Radio.Group
                optionType="button"
                options={[
                  { value: 'full', label: t('invoice.paidInFull') },
                  { value: 'partial', label: t('invoice.partial') },
                  { value: 'none', label: t('invoice.notPaid') },
                ]}
                data-testid="payment-mode"
              />
            </Form.Item>
            {paymentMode !== 'none' && (
              <Row gutter={16}>
                <Col xs={24} md={8}>
                  <Form.Item name="paymentMethod" label={t('invoice.method')}>
                    <Select options={PAYMENT_METHODS.map((m) => ({ value: m, label: t(`invoice.${m}`) }))} />
                  </Form.Item>
                </Col>
                {paymentMode === 'partial' && (
                  <Col xs={24} md={8}>
                    <Form.Item name="paymentAmount" label={t('invoice.amount')} rules={[{ required: true, message: t('common.required') }]}>
                      <InputNumber min={0.01} precision={2} style={{ width: '100%' }} />
                    </Form.Item>
                  </Col>
                )}
                <Col xs={24} md={8}>
                  <Form.Item name="paymentReference" label={t('invoice.reference')}>
                    <Input maxLength={100} />
                  </Form.Item>
                </Col>
              </Row>
            )}
          </Card>
        )}
        <Typography.Paragraph type="secondary" />
        <Button type="primary" htmlType="submit" size="large" loading={saving} data-testid="document-submit">
          {mode === 'invoice' && !existing ? t('invoice.issue') : t('common.save')}
        </Button>
        <Button size="large" style={{ marginInlineStart: 8 }} onClick={() => navigate(-1)}>
          {t('common.cancel')}
        </Button>
      </Form>
    </>
  );
}
