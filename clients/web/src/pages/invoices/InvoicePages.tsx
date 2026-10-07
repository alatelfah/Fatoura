import { DeleteOutlined, DollarOutlined, EditOutlined, FilePdfOutlined, PlusOutlined, RollbackOutlined, StopOutlined } from '@ant-design/icons';
import { Alert, App, Button, Card, Checkbox, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Select, Skeleton, Switch, Table, Tag } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router';
import { $api, fetchClient, openPdf, type Schemas } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { DocumentView } from '../../components/DocumentView';
import { Ltr, Money } from '../../components/Ltr';
import { PageHeader } from '../../components/PageHeader';
import { applyProblem } from '../../components/problems';
import { formatDate, formatQty, isoDate } from '../../utils/format';
import { PAYMENT_METHODS, SalesDocumentForm } from '../quotations/SalesDocumentForm';

export function InvoiceStatusTag({ status, balance }: { status: Schemas['InvoiceStatus']; balance: number }) {
  const { t } = useTranslation();
  if (status === 'Void') return <Tag color="red">{t('invoice.Void')}</Tag>;
  if (balance <= 0) return <Tag color="green">{t('invoice.paidInFull')}</Tag>;
  return <Tag color="gold">{t('doc.balance')}</Tag>;
}

export function InvoicesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [unpaidOnly, setUnpaidOnly] = useState(false);
  const list = $api.useQuery('get', '/api/invoices', { params: { query: { search: search || undefined, page, pageSize: 20, unpaidOnly: unpaidOnly || undefined } } });
  return (
    <>
      <PageHeader
        title={t('invoice.title')}
        extra={
          <>
            <Input.Search allowClear placeholder={t('common.search')} onSearch={(v) => { setSearch(v); setPage(1); }} style={{ width: 240 }} data-testid="invoice-search" />
            <Checkbox checked={unpaidOnly} onChange={(e) => setUnpaidOnly(e.target.checked)}>{t('invoice.unpaidOnly')}</Checkbox>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/invoices/new')} data-testid="invoice-new">{t('invoice.new')}</Button>
          </>
        }
      />
      <Table
        rowKey="id"
        loading={list.isLoading}
        dataSource={list.data?.items}
        scroll={{ x: 1000 }}
        onRow={(r) => ({ onClick: () => navigate(`/invoices/${r.id}`), style: { cursor: 'pointer' } })}
        pagination={{ current: page, pageSize: 20, total: list.data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: t('doc.number'), dataIndex: 'number', render: (v: string) => <Ltr>{v}</Ltr> },
          { title: t('doc.date'), dataIndex: 'date', render: formatDate },
          { title: t('doc.client'), dataIndex: 'clientName' },
          { title: t('common.total'), dataIndex: 'total', className: 'num', render: (v: number, r) => <Money value={v} currency={r.currency} /> },
          { title: t('doc.paid'), dataIndex: 'paidTotal', className: 'num', render: (v: number, r) => <Money value={v} currency={r.currency} /> },
          { title: t('doc.balance'), dataIndex: 'balance', className: 'num', render: (v: number, r) => <Money value={v} currency={r.currency} /> },
          { title: t('common.status'), dataIndex: 'status', render: (s: Schemas['InvoiceStatus'], r) => <InvoiceStatusTag status={s} balance={r.balance} /> },
          { title: t('doc.createdBy'), dataIndex: 'createdByName' },
        ]}
      />
    </>
  );
}

export function InvoiceNewPage() {
  return <SalesDocumentForm mode="invoice" />;
}

export function InvoiceEditPage() {
  const id = Number(useParams().id);
  const q = $api.useQuery('get', '/api/invoices/{id}', { params: { path: { id } } });
  return q.data ? <SalesDocumentForm mode="invoice" existing={q.data} /> : <Skeleton active />;
}

export function InvoiceViewPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const { isAdmin } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const id = Number(useParams().id);
  const { data: inv, refetch } = $api.useQuery('get', '/api/invoices/{id}', { params: { path: { id } } });
  const [paymentOpen, setPaymentOpen] = useState(false);
  const [voidOpen, setVoidOpen] = useState(false);
  const [creditOpen, setCreditOpen] = useState(false);
  if (!inv) return <Skeleton active />;

  const reload = async () => {
    await refetch();
    await queryClient.invalidateQueries({ queryKey: ['get', '/api/invoices'] });
  };
  const issued = inv.status === 'Issued';
  const removePayment = async (paymentId: number) => {
    const { error } = await fetchClient.DELETE('/api/invoices/{id}/payments/{paymentId}', { params: { path: { id, paymentId } } });
    if (error) message.error(applyProblem(error));
    else await reload();
  };

  return (
    <>
      <PageHeader
        title={<>{t('nav.invoices')} <Ltr>{inv.number}</Ltr></>}
        subtitle={<InvoiceStatusTag status={inv.status} balance={inv.balance} />}
        extra={
          <>
            <Button icon={<FilePdfOutlined />} onClick={() => openPdf(`/api/invoices/${id}/pdf`)} data-testid="invoice-pdf">{t('common.print')}</Button>
            {issued && inv.balance > 0 && <Button icon={<DollarOutlined />} onClick={() => setPaymentOpen(true)} data-testid="invoice-add-payment">{t('invoice.addPayment')}</Button>}
            {issued && <Button icon={<RollbackOutlined />} onClick={() => setCreditOpen(true)} data-testid="invoice-credit-note">{t('invoice.creditNote')}</Button>}
            {isAdmin && issued && inv.creditNotes.length === 0 && <Button icon={<EditOutlined />} onClick={() => navigate(`/invoices/${id}/edit`)}>{t('common.edit')}</Button>}
            {isAdmin && issued && inv.creditNotes.length === 0 && <Button danger icon={<StopOutlined />} onClick={() => setVoidOpen(true)} data-testid="invoice-void">{t('invoice.void')}</Button>}
          </>
        }
      />
      {inv.status === 'Void' && <Alert type="error" showIcon style={{ marginBottom: 16 }} title={`${t('invoice.Void')}: ${inv.voidReason}`} />}
      <DocumentView
        number={inv.number}
        date={inv.date}
        client={inv.client}
        info={[
          { label: t('doc.createdBy'), value: inv.createdByName },
          ...(inv.quotationId ? [{ label: t('invoice.quotationRef'), value: <Link to={`/quotations/${inv.quotationId}`}><Ltr>{inv.quotationNumber}</Ltr></Link> }] : []),
          { label: t('doc.paid'), value: <Money value={inv.paidTotal} currency={inv.currency.code} /> },
          ...(inv.creditedTotal > 0 ? [{ label: t('doc.credited'), value: <Money value={inv.creditedTotal} currency={inv.currency.code} /> }] : []),
          { label: t('doc.balance'), value: <span data-testid="invoice-balance"><Money value={inv.balance} currency={inv.currency.code} strong /></span> },
        ]}
        lines={inv.lines}
        discount={inv.discount.amount}
        discountLabel={inv.discount.kind === 'Percent' ? `${inv.discount.value}%` : undefined}
        subTotal={inv.subTotal}
        vatTotal={inv.vatTotal}
        total={inv.total}
        currency={inv.currency}
        terms={inv.terms}
      />
      <Card title={t('invoice.payments')} style={{ marginTop: 16 }} size="small">
        <Table
          size="small"
          rowKey="id"
          pagination={false}
          dataSource={inv.payments}
          locale={{ emptyText: t('common.noData') }}
          columns={[
            { title: t('doc.date'), dataIndex: 'date', render: formatDate },
            { title: t('invoice.method'), dataIndex: 'method', render: (m: string) => t(`invoice.${m}`) },
            { title: t('invoice.reference'), dataIndex: 'reference' },
            { title: t('invoice.amount'), dataIndex: 'amount', className: 'num', render: (v: number) => <Money value={v} currency={inv.currency.code} /> },
            ...(isAdmin
              ? [{ key: 'del', width: 50, render: (_: unknown, p: Schemas['PaymentDto']) => (
                  <Popconfirm title={t('common.delete') + '?'} onConfirm={() => removePayment(p.id)} okText={t('common.yes')} cancelText={t('common.no')}>
                    <Button type="text" danger icon={<DeleteOutlined />} />
                  </Popconfirm>
                ) }]
              : []),
          ]}
        />
      </Card>
      {inv.creditNotes.length > 0 && (
        <Card title={t('nav.creditNotes')} style={{ marginTop: 16 }} size="small">
          <Table
            size="small"
            rowKey="id"
            pagination={false}
            dataSource={inv.creditNotes}
            columns={[
              { title: t('doc.number'), dataIndex: 'number', render: (v: string, c) => <Link to={`/credit-notes/${c.id}`}><Ltr>{v}</Ltr></Link> },
              { title: t('doc.date'), dataIndex: 'date', render: formatDate },
              { title: t('creditNote.reason'), dataIndex: 'reason' },
              { title: t('common.total'), dataIndex: 'total', className: 'num', render: (v: number) => <Money value={v} currency={inv.currency.code} /> },
            ]}
          />
        </Card>
      )}
      <PaymentModal open={paymentOpen} invoice={inv} onClose={() => setPaymentOpen(false)} onDone={reload} />
      <VoidModal open={voidOpen} invoiceId={id} onClose={() => setVoidOpen(false)} onDone={reload} />
      <CreditNoteModal open={creditOpen} invoice={inv} onClose={() => setCreditOpen(false)} onDone={async (noteId) => { await reload(); navigate(`/credit-notes/${noteId}`); }} />
    </>
  );
}

function PaymentModal({ open, invoice, onClose, onDone }: { open: boolean; invoice: Schemas['InvoiceDto']; onClose: () => void; onDone: () => Promise<void> }) {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const submit = async () => {
    const v = await form.validateFields().catch(() => null);
    if (!v) return;
    const { error } = await fetchClient.POST('/api/invoices/{id}/payments', {
      params: { path: { id: invoice.id } },
      body: { amount: v.amount, method: v.method, date: isoDate(v.date) ?? null, reference: v.reference ?? null },
    });
    if (error) return void message.error(applyProblem(error, form));
    onClose();
    form.resetFields();
    await onDone();
  };
  return (
    <Modal open={open} title={t('invoice.addPayment')} onOk={submit} onCancel={onClose} okText={t('common.save')} cancelText={t('common.cancel')} destroyOnHidden>
      <Form form={form} layout="vertical" initialValues={{ amount: invoice.balance, method: 'Cash', date: dayjs() }}>
        <Form.Item name="amount" label={t('invoice.amount')} rules={[{ required: true, message: t('common.required') }]}>
          <InputNumber min={0.01} max={invoice.balance} precision={2} style={{ width: '100%' }} data-testid="payment-amount" />
        </Form.Item>
        <Form.Item name="method" label={t('invoice.method')}>
          <Select options={PAYMENT_METHODS.map((m) => ({ value: m, label: t(`invoice.${m}`) }))} />
        </Form.Item>
        <Form.Item name="date" label={t('doc.date')}>
          <DatePicker style={{ width: '100%' }} disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
        </Form.Item>
        <Form.Item name="reference" label={t('invoice.reference')}>
          <Input maxLength={100} />
        </Form.Item>
      </Form>
    </Modal>
  );
}

function VoidModal({ open, invoiceId, onClose, onDone }: { open: boolean; invoiceId: number; onClose: () => void; onDone: () => Promise<void> }) {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const submit = async () => {
    const v = await form.validateFields().catch(() => null);
    if (!v) return;
    const { error } = await fetchClient.POST('/api/invoices/{id}/void', { params: { path: { id: invoiceId } }, body: { reason: v.reason } });
    if (error) return void message.error(applyProblem(error, form));
    message.success(t('invoice.voided'));
    onClose();
    await onDone();
  };
  return (
    <Modal open={open} title={t('invoice.void')} onOk={submit} onCancel={onClose} okText={t('invoice.void')} okButtonProps={{ danger: true, 'data-testid': 'void-confirm' } as never} cancelText={t('common.cancel')} destroyOnHidden>
      <Alert type="warning" showIcon title={t('invoice.voidConfirm')} style={{ marginBottom: 16 }} />
      <Form form={form} layout="vertical">
        <Form.Item name="reason" label={t('invoice.voidReason')} rules={[{ required: true, whitespace: true, message: t('common.required') }]}>
          <Input.TextArea maxLength={500} data-testid="void-reason" />
        </Form.Item>
      </Form>
    </Modal>
  );
}

function CreditNoteModal({ open, invoice, onClose, onDone }: { open: boolean; invoice: Schemas['InvoiceDto']; onClose: () => void; onDone: (id: number) => Promise<void> }) {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [qty, setQty] = useState<Record<number, number | null>>({});
  const submit = async () => {
    const v = await form.validateFields().catch(() => null);
    if (!v) return;
    const lines = invoice.lines.filter((l) => (qty[l.id] ?? 0) > 0).map((l) => ({ invoiceLineId: l.id, quantity: Number(qty[l.id]) }));
    if (lines.length === 0) return void message.error(t('doc.noLines'));
    const { data, error } = await fetchClient.POST('/api/credit-notes', {
      body: { invoiceId: invoice.id, reason: v.reason, returnToStock: v.returnToStock ?? true, date: null, lines },
    });
    if (error) return void message.error(applyProblem(error, form));
    message.success(t('creditNote.issued', { number: data!.number }));
    onClose();
    setQty({});
    await onDone(data!.id);
  };
  return (
    <Modal open={open} width={760} title={t('invoice.creditNote')} onOk={submit} onCancel={onClose} okText={t('common.create')} okButtonProps={{ 'data-testid': 'credit-confirm' } as never} cancelText={t('common.cancel')} destroyOnHidden>
      <Table
        size="small"
        rowKey="id"
        pagination={false}
        dataSource={invoice.lines}
        columns={[
          { title: t('doc.description'), dataIndex: 'description', ellipsis: true },
          { title: t('doc.unitPriceIn', { currency: invoice.currency.code }), dataIndex: 'unitPrice', className: 'num', render: (v: number) => <Money value={v} /> },
          { title: t('creditNote.remaining'), key: 'remaining', className: 'num', render: (_: unknown, l) => <Ltr>{formatQty(l.quantity - l.creditedQuantity)}</Ltr> },
          {
            title: t('creditNote.creditQty'),
            key: 'qty',
            width: 130,
            render: (_: unknown, l, index) => (
              <InputNumber
                min={0}
                max={l.quantity - l.creditedQuantity}
                precision={3}
                value={qty[l.id] ?? null}
                onChange={(v) => setQty((s) => ({ ...s, [l.id]: v }))}
                disabled={l.quantity - l.creditedQuantity <= 0}
                data-testid={`credit-qty-${index}`}
              />
            ),
          },
        ]}
      />
      <Form form={form} layout="vertical" style={{ marginTop: 16 }} initialValues={{ returnToStock: true }}>
        <Form.Item name="reason" label={t('creditNote.reason')} rules={[{ required: true, whitespace: true, message: t('common.required') }]}>
          <Input maxLength={500} data-testid="credit-reason" />
        </Form.Item>
        <Form.Item name="returnToStock" label={t('creditNote.returnToStock')} valuePropName="checked">
          <Switch />
        </Form.Item>
      </Form>
    </Modal>
  );
}
