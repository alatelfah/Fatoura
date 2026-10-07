import { FilePdfOutlined } from '@ant-design/icons';
import { Input, Skeleton, Table } from 'antd';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router';
import { $api, openPdf } from '../../api/client';
import { DocumentView } from '../../components/DocumentView';
import { Ltr, Money } from '../../components/Ltr';
import { PageHeader } from '../../components/PageHeader';
import { formatDate } from '../../utils/format';
import { Button } from 'antd';

export function CreditNotesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const list = $api.useQuery('get', '/api/credit-notes', { params: { query: { search: search || undefined, page, pageSize: 20 } } });
  return (
    <>
      <PageHeader title={t('creditNote.title')} extra={<Input.Search allowClear placeholder={t('common.search')} onSearch={(v) => { setSearch(v); setPage(1); }} style={{ width: 240 }} />} />
      <Table
        rowKey="id"
        loading={list.isLoading}
        dataSource={list.data?.items}
        scroll={{ x: 800 }}
        onRow={(r) => ({ onClick: () => navigate(`/credit-notes/${r.id}`), style: { cursor: 'pointer' } })}
        pagination={{ current: page, pageSize: 20, total: list.data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: t('doc.number'), dataIndex: 'number', render: (v: string) => <Ltr>{v}</Ltr> },
          { title: t('doc.date'), dataIndex: 'date', render: formatDate },
          { title: t('creditNote.againstInvoice'), dataIndex: 'invoiceNumber', render: (v: string) => <Ltr>{v}</Ltr> },
          { title: t('doc.client'), dataIndex: 'clientName' },
          { title: t('common.total'), dataIndex: 'total', className: 'num', render: (v: number) => <Money value={v} /> },
          { title: t('doc.createdBy'), dataIndex: 'createdByName' },
        ]}
      />
    </>
  );
}

export function CreditNoteViewPage() {
  const { t } = useTranslation();
  const id = Number(useParams().id);
  const { data: c } = $api.useQuery('get', '/api/credit-notes/{id}', { params: { path: { id } } });
  if (!c) return <Skeleton active />;
  return (
    <>
      <PageHeader
        title={<>{t('nav.creditNotes')} <Ltr>{c.number}</Ltr></>}
        extra={<Button icon={<FilePdfOutlined />} onClick={() => openPdf(`/api/credit-notes/${id}/pdf`)}>{t('common.print')}</Button>}
      />
      <DocumentView
        number={c.number}
        date={c.date}
        client={c.client}
        info={[
          { label: t('creditNote.againstInvoice'), value: <Link to={`/invoices/${c.invoiceId}`}><Ltr>{c.invoiceNumber}</Ltr></Link> },
          { label: t('creditNote.reason'), value: c.reason },
          { label: t('creditNote.returnToStock'), value: c.returnToStock ? t('common.yes') : t('common.no') },
          { label: t('doc.createdBy'), value: c.createdByName },
        ]}
        lines={c.lines}
        discount={c.discount}
        subTotal={c.subTotal}
        vatTotal={c.vatTotal}
        total={c.total}
      />
    </>
  );
}
