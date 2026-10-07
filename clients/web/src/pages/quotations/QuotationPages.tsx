import { CheckCircleOutlined, EditOutlined, FilePdfOutlined, PlusOutlined, SwapOutlined } from '@ant-design/icons';
import { App, Button, Checkbox, Dropdown, Input, Popconfirm, Skeleton, Table, Tag } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router';
import { $api, fetchClient, openPdf, type Schemas } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { DocumentView } from '../../components/DocumentView';
import { Ltr, Money } from '../../components/Ltr';
import { PageHeader } from '../../components/PageHeader';
import { applyProblem } from '../../components/problems';
import { formatDate } from '../../utils/format';
import { SalesDocumentForm } from './SalesDocumentForm';

const STATUS_COLORS: Record<Schemas['QuotationStatus'], string> = { Draft: 'default', Sent: 'blue', Accepted: 'green', Rejected: 'red', Converted: 'purple' };

export function QuotationStatusTag({ status, expired }: { status: Schemas['QuotationStatus']; expired?: boolean }) {
  const { t } = useTranslation();
  return (
    <>
      <Tag color={STATUS_COLORS[status]}>{t(`quotation.${status}`)}</Tag>
      {expired && <Tag color="orange">{t('doc.expired')}</Tag>}
    </>
  );
}

export function QuotationsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [openOnly, setOpenOnly] = useState(false);
  const list = $api.useQuery('get', '/api/quotations', { params: { query: { search: search || undefined, page, pageSize: 20, openOnly: openOnly || undefined } } });
  return (
    <>
      <PageHeader
        title={t('quotation.title')}
        extra={
          <>
            <Input.Search allowClear placeholder={t('common.search')} onSearch={(v) => { setSearch(v); setPage(1); }} style={{ width: 240 }} />
            <Checkbox checked={openOnly} onChange={(e) => setOpenOnly(e.target.checked)}>{t('quotation.openOnly')}</Checkbox>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/quotations/new')} data-testid="quotation-new">{t('quotation.new')}</Button>
          </>
        }
      />
      <Table
        rowKey="id"
        loading={list.isLoading}
        dataSource={list.data?.items}
        scroll={{ x: 900 }}
        onRow={(r) => ({ onClick: () => navigate(`/quotations/${r.id}`), style: { cursor: 'pointer' } })}
        pagination={{ current: page, pageSize: 20, total: list.data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: t('doc.number'), dataIndex: 'number', render: (v: string) => <Ltr>{v}</Ltr> },
          { title: t('doc.date'), dataIndex: 'date', render: formatDate },
          { title: t('doc.client'), dataIndex: 'clientName' },
          { title: t('doc.validUntil'), dataIndex: 'validUntil', render: formatDate },
          { title: t('common.status'), dataIndex: 'status', render: (s: Schemas['QuotationStatus'], r) => <QuotationStatusTag status={s} expired={r.isExpired} /> },
          { title: t('common.total'), dataIndex: 'total', className: 'num', render: (v: number) => <Money value={v} /> },
          { title: t('doc.createdBy'), dataIndex: 'createdByName' },
        ]}
      />
    </>
  );
}

export function QuotationNewPage() {
  return <SalesDocumentForm mode="quotation" />;
}

export function QuotationEditPage() {
  const id = Number(useParams().id);
  const q = $api.useQuery('get', '/api/quotations/{id}', { params: { path: { id } } });
  return q.data ? <SalesDocumentForm mode="quotation" existing={q.data} /> : <Skeleton active />;
}

export function QuotationViewPage() {
  const { t } = useTranslation();
  const { message, modal, notification } = App.useApp();
  const { user, isAdmin } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const id = Number(useParams().id);
  const { data: q, refetch } = $api.useQuery('get', '/api/quotations/{id}', { params: { path: { id } } });
  if (!q) return <Skeleton active />;

  const canChange = q.status !== 'Converted' && (isAdmin || q.createdById === user?.id);

  const setStatus = async (status: Schemas['QuotationStatus']) => {
    const { error } = await fetchClient.PUT('/api/quotations/{id}/status', { params: { path: { id } }, body: { status } });
    if (error) message.error(applyProblem(error));
    else await refetch();
  };

  const convert = () =>
    modal.confirm({
      title: t('quotation.convert'),
      content: t('quotation.convertConfirm'),
      okText: t('common.confirm'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        const { data, error } = await fetchClient.POST('/api/quotations/{id}/convert', { params: { path: { id } }, body: { payment: null } });
        if (error) return void message.error(applyProblem(error));
        for (const w of data!.warnings) notification.warning({ title: t('item.lowStock'), description: t('doc.stockWarning', { name: w.itemName, qty: w.stockAfter }) });
        message.success(t('quotation.converted', { number: data!.invoice.number }));
        await queryClient.invalidateQueries();
        navigate(`/invoices/${data!.invoice.id}`);
      },
    });

  const remove = async () => {
    const { error } = await fetchClient.DELETE('/api/quotations/{id}', { params: { path: { id } } });
    if (error) return void message.error(applyProblem(error));
    await queryClient.invalidateQueries({ queryKey: ['get', '/api/quotations'] });
    navigate('/quotations');
  };

  return (
    <>
      <PageHeader
        title={<>{t('nav.quotations')} <Ltr>{q.number}</Ltr></>}
        subtitle={<QuotationStatusTag status={q.status} expired={q.isExpired} />}
        extra={
          <>
            <Button icon={<FilePdfOutlined />} onClick={() => openPdf(`/api/quotations/${id}/pdf`)} data-testid="quotation-pdf">{t('common.print')}</Button>
            {canChange && <Button icon={<EditOutlined />} onClick={() => navigate(`/quotations/${id}/edit`)}>{t('common.edit')}</Button>}
            {canChange && (
              <Dropdown menu={{ items: (['Draft', 'Sent', 'Accepted', 'Rejected'] as const).filter((s) => s !== q.status).map((s) => ({ key: s, label: t(`quotation.${s}`), onClick: () => setStatus(s) })) }}>
                <Button icon={<CheckCircleOutlined />}>{t('quotation.markAs')}</Button>
              </Dropdown>
            )}
            {q.status !== 'Converted' && q.status !== 'Rejected' && (
              <Button type="primary" icon={<SwapOutlined />} onClick={convert} data-testid="quotation-convert">{t('quotation.convert')}</Button>
            )}
            {isAdmin && q.status !== 'Converted' && (
              <Popconfirm title={t('common.delete') + '?'} onConfirm={remove} okText={t('common.yes')} cancelText={t('common.no')}>
                <Button danger>{t('common.delete')}</Button>
              </Popconfirm>
            )}
          </>
        }
      />
      <DocumentView
        number={q.number}
        date={q.date}
        client={q.client}
        info={[
          { label: t('doc.validUntil'), value: formatDate(q.validUntil) },
          { label: t('doc.createdBy'), value: q.createdByName },
          ...(q.convertedInvoiceId ? [{ label: t('quotation.invoice'), value: <Link to={`/invoices/${q.convertedInvoiceId}`}><Ltr>{q.convertedInvoiceNumber}</Ltr></Link> }] : []),
        ]}
        lines={q.lines}
        discount={q.discount.amount}
        discountLabel={q.discount.kind === 'Percent' ? `${q.discount.value}%` : undefined}
        subTotal={q.subTotal}
        vatTotal={q.vatTotal}
        total={q.total}
        terms={q.terms}
      />
    </>
  );
}
