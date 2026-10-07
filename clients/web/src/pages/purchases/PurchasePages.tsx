import { DeleteOutlined, EditOutlined, PaperClipOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons';
import { App, Button, Card, Col, DatePicker, Form, Input, Popconfirm, Row, Skeleton, Table, Upload } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router';
import { $api, downloadFile, fetchClient, uploadFile } from '../../api/client';
import { ContactSelect } from '../../components/ClientSelect';
import { CurrencyFields, fromCurrency, toCurrencyRequest, type CurrencyFormValue } from '../../components/CurrencyFields';
import { DocumentLinesEditor, emptyLine, type LineFormValue } from '../../components/DocumentLinesEditor';
import { DocumentView } from '../../components/DocumentView';
import { Ltr, Money } from '../../components/Ltr';
import { PageHeader } from '../../components/PageHeader';
import { applyProblem } from '../../components/problems';
import { formatDate, isoDate } from '../../utils/format';

export function PurchasesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const list = $api.useQuery('get', '/api/purchases', { params: { query: { search: search || undefined, page, pageSize: 20 } } });
  return (
    <>
      <PageHeader
        title={t('purchase.title')}
        extra={
          <>
            <Input.Search allowClear placeholder={t('common.search')} onSearch={(v) => { setSearch(v); setPage(1); }} style={{ width: 240 }} />
            <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/purchases/new')} data-testid="purchase-new">{t('purchase.new')}</Button>
          </>
        }
      />
      <Table
        rowKey="id"
        loading={list.isLoading}
        dataSource={list.data?.items}
        scroll={{ x: 900 }}
        onRow={(r) => ({ onClick: () => navigate(`/purchases/${r.id}`), style: { cursor: 'pointer' } })}
        pagination={{ current: page, pageSize: 20, total: list.data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: t('doc.number'), dataIndex: 'number', render: (v: string) => <Ltr>{v}</Ltr> },
          { title: t('purchase.supplierInvoiceNo'), dataIndex: 'supplierInvoiceNo', render: (v: string) => <Ltr>{v}</Ltr> },
          { title: t('doc.date'), dataIndex: 'date', render: formatDate },
          { title: t('doc.supplier'), dataIndex: 'supplierName' },
          { title: t('reports.net'), dataIndex: 'subTotal', className: 'num', render: (v: number) => <Money value={v} /> },
          { title: t('reports.vatAmount'), dataIndex: 'vatTotal', className: 'num', render: (v: number) => <Money value={v} /> },
          { title: t('common.total'), dataIndex: 'total', className: 'num', render: (v: number, r) => <Money value={v} currency={r.currency} /> },
        ]}
      />
    </>
  );
}

interface PurchaseForm extends CurrencyFormValue {
  supplierId: number;
  supplierInvoiceNo?: string;
  date: Dayjs;
  notes?: string;
  lines: LineFormValue[];
}

export function PurchaseFormPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const params = useParams();
  const id = params.id ? Number(params.id) : null;
  const existing = $api.useQuery('get', '/api/purchases/{id}', { params: { path: { id: id ?? 0 } } }, { enabled: id !== null });
  const settings = $api.useQuery('get', '/api/settings');
  const [form] = Form.useForm<PurchaseForm>();
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (id === null) form.setFieldsValue({ date: dayjs(), currency: 'AED', lines: [{ ...emptyLine }] });
    else if (existing.data) {
      const p = existing.data;
      form.setFieldsValue({
        supplierId: p.supplierId,
        supplierInvoiceNo: p.supplierInvoiceNo,
        date: dayjs(p.date),
        notes: p.notes,
        ...fromCurrency(p.currency),
        lines: p.lines.map((l) => ({ itemId: l.itemId ?? null, description: l.description, expenseCategory: l.expenseCategory, quantity: l.quantity, unitPrice: l.unitPrice, taxCategory: l.taxCategory })),
      });
    }
  }, [id, existing.data, form]);

  if (id !== null && !existing.data) return <Skeleton active />;

  const submit = async (v: PurchaseForm) => {
    setSaving(true);
    try {
      const body = {
        supplierId: v.supplierId,
        supplierInvoiceNo: v.supplierInvoiceNo ?? null,
        date: isoDate(v.date)!,
        notes: v.notes ?? null,
        ...toCurrencyRequest(v),
        lines: v.lines.map((l) => ({
          itemId: l.itemId ?? null,
          description: (l.description ?? '').trim(),
          expenseCategory: l.itemId ? null : (l.expenseCategory ?? null),
          quantity: Number(l.quantity ?? 0),
          unitPrice: Number(l.unitPrice ?? 0),
          taxCategory: l.taxCategory ?? 'Standard',
        })),
      };
      const { data, error } = id === null
        ? await fetchClient.POST('/api/purchases', { body })
        : await fetchClient.PUT('/api/purchases/{id}', { params: { path: { id } }, body });
      if (error) return void message.error(applyProblem(error, form));
      message.success(t('common.saved'));
      await queryClient.invalidateQueries();
      navigate(`/purchases/${data!.id}`);
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <PageHeader title={id === null ? t('purchase.new') : t('purchase.edit')} />
      <Form form={form} layout="vertical" onFinish={submit}>
        <Card style={{ marginBottom: 16 }}>
          <Row gutter={16}>
            <Col xs={24} md={8}>
              <Form.Item name="supplierId" label={t('doc.supplier')} rules={[{ required: true, message: t('common.required') }]}>
                <ContactSelect kind="suppliers" initialLabel={existing.data?.supplier.name} />
              </Form.Item>
            </Col>
            <Col xs={12} md={3}>
              <Form.Item name="supplierInvoiceNo" label={t('purchase.supplierInvoiceNo')}>
                <Input maxLength={60} dir="ltr" />
              </Form.Item>
            </Col>
            <Col xs={12} md={3}>
              <Form.Item name="date" label={t('doc.date')} rules={[{ required: true, message: t('common.required') }]}>
                <DatePicker style={{ width: '100%' }} disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
              </Form.Item>
            </Col>
            <CurrencyFields form={form} existing={existing.data?.currency.code} />
          </Row>
        </Card>
        <Card title={t('doc.lines')} style={{ marginBottom: 16 }}>
          <DocumentLinesEditor form={form} vatRate={settings.data?.vatRate ?? 0.05} purchase />
        </Card>
        <Form.Item name="notes" label={t('common.notes')}>
          <Input.TextArea maxLength={2000} autoSize={{ minRows: 2 }} />
        </Form.Item>
        <Button type="primary" htmlType="submit" size="large" loading={saving} data-testid="purchase-submit">{t('common.save')}</Button>
        <Button size="large" style={{ marginInlineStart: 8 }} onClick={() => navigate(-1)}>{t('common.cancel')}</Button>
      </Form>
    </>
  );
}

export function PurchaseViewPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const id = Number(useParams().id);
  const { data: p, refetch } = $api.useQuery('get', '/api/purchases/{id}', { params: { path: { id } } });
  if (!p) return <Skeleton active />;

  const remove = async () => {
    const { error } = await fetchClient.DELETE('/api/purchases/{id}', { params: { path: { id } } });
    if (error) return void message.error(applyProblem(error));
    await queryClient.invalidateQueries();
    navigate('/purchases');
  };

  const upload = async (file: File) => {
    const error = await uploadFile(`/api/purchases/${id}/attachment`, file);
    if (error) message.error(applyProblem(error));
    else await refetch();
  };

  return (
    <>
      <PageHeader
        title={<>{t('nav.purchases')} <Ltr>{p.number}</Ltr></>}
        extra={
          <>
            {p.hasAttachment && (
              <Button icon={<PaperClipOutlined />} onClick={() => downloadFile(`/api/purchases/${id}/attachment`, p.attachmentFileName ?? 'attachment')}>
                {p.attachmentFileName ?? t('purchase.attachment')}
              </Button>
            )}
            <Upload showUploadList={false} accept=".pdf,.png,.jpg,.jpeg" beforeUpload={(f) => { void upload(f); return false; }}>
              <Button icon={<UploadOutlined />}>{t('purchase.attachment')}</Button>
            </Upload>
            <Button icon={<EditOutlined />} onClick={() => navigate(`/purchases/${id}/edit`)}>{t('common.edit')}</Button>
            <Popconfirm title={t('purchase.deleteConfirm')} onConfirm={remove} okText={t('common.yes')} cancelText={t('common.no')}>
              <Button danger icon={<DeleteOutlined />}>{t('common.delete')}</Button>
            </Popconfirm>
          </>
        }
      />
      <DocumentView
        number={p.number}
        date={p.date}
        client={p.supplier}
        info={[{ label: t('purchase.supplierInvoiceNo'), value: <Ltr>{p.supplierInvoiceNo || '—'}</Ltr> }]}
        lines={p.lines.map((l) => ({ ...l, description: l.expenseCategory ? `${l.description} (${l.expenseCategory})` : l.description }))}
        subTotal={p.subTotal}
        vatTotal={p.vatTotal}
        total={p.total}
        currency={p.currency}
      />
    </>
  );
}
