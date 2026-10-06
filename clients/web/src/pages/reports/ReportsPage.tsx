import { DownloadOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Col, DatePicker, Descriptions, Flex, Row, Select, Statistic, Table, Tabs, Tag } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { $api, downloadFile } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { ContactSelect } from '../../components/ClientSelect';
import { Ltr, Money } from '../../components/Ltr';
import { PageHeader } from '../../components/PageHeader';
import { formatDate, formatMoney, isoDate } from '../../utils/format';

type Range = [Dayjs, Dayjs];

function useRange(): [Range, (r: Range) => void] {
  return useState<Range>([dayjs().startOf('month'), dayjs()]);
}

function query(range: Range, extra: Record<string, string | number | undefined> = {}) {
  const q = new URLSearchParams();
  q.set('from', isoDate(range[0])!);
  q.set('to', isoDate(range[1])!);
  for (const [k, v] of Object.entries(extra)) if (v !== undefined && v !== '') q.set(k, String(v));
  return q.toString();
}

function RangeBar({ range, onChange, exportPath, children }: { range: Range; onChange: (r: Range) => void; exportPath: string; children?: React.ReactNode }) {
  const { t } = useTranslation();
  return (
    <Flex gap={8} wrap style={{ marginBottom: 16 }}>
      <DatePicker.RangePicker
        value={range}
        allowClear={false}
        onChange={(v) => v?.[0] && v[1] && onChange([v[0], v[1]])}
        presets={[
          { label: dayjs().format('MMMM YYYY'), value: [dayjs().startOf('month'), dayjs()] },
          { label: dayjs().subtract(1, 'month').format('MMMM YYYY'), value: [dayjs().subtract(1, 'month').startOf('month'), dayjs().subtract(1, 'month').endOf('month')] },
          { label: `Q${Math.floor(dayjs().month() / 3) + 1} ${dayjs().year()}`, value: [dayjs().startOf('month').subtract(dayjs().month() % 3, 'month'), dayjs()] },
          { label: String(dayjs().year()), value: [dayjs().startOf('year'), dayjs()] },
        ]}
      />
      {children}
      <Button icon={<DownloadOutlined />} onClick={() => downloadFile(exportPath, 'report.xlsx')}>{t('common.export')}</Button>
    </Flex>
  );
}

function SalesReport() {
  const { t } = useTranslation();
  const { isAdmin } = useAuth();
  const [range, setRange] = useRange();
  const [clientId, setClientId] = useState<number | undefined>();
  const [cashierId, setCashierId] = useState<string | undefined>();
  const users = $api.useQuery('get', '/api/users', {}, { enabled: isAdmin });
  const params = { from: isoDate(range[0]), to: isoDate(range[1]), clientId, cashierId };
  const { data } = $api.useQuery('get', '/api/reports/sales', { params: { query: params } });
  return (
    <>
      <RangeBar range={range} onChange={setRange} exportPath={`/api/reports/sales/export?${query(range, { clientId, cashierId })}`}>
        <div style={{ width: 240 }}><ContactSelect value={clientId} onChange={setClientId} placeholder={t('doc.client')} /></div>
        {clientId && <Button onClick={() => setClientId(undefined)}>{t('common.all')}</Button>}
        {isAdmin && (
          <Select allowClear placeholder={t('reports.cashier')} style={{ width: 200 }} value={cashierId} onChange={setCashierId}
            options={(users.data ?? []).map((u) => ({ value: u.id, label: u.displayName }))} />
        )}
      </RangeBar>
      {data && (
        <>
          <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
            <Col xs={12} md={6}><Card><Statistic title={t('reports.invoices')} value={data.summary.invoiceCount} /></Card></Col>
            <Col xs={12} md={6}><Card><Statistic title={t('reports.netSales')} value={data.summary.netSales} formatter={(v) => <Ltr>{formatMoney(Number(v))}</Ltr>} /></Card></Col>
            <Col xs={12} md={6}><Card><Statistic title={t('reports.netVat')} value={data.summary.netVat} formatter={(v) => <Ltr>{formatMoney(Number(v))}</Ltr>} /></Card></Col>
            <Col xs={12} md={6}><Card><Statistic title={t('reports.netTotal')} value={data.summary.netTotal} formatter={(v) => <Ltr>{formatMoney(Number(v))}</Ltr>} data-testid="sales-net-total" /></Card></Col>
          </Row>
          <Table
            size="small"
            rowKey={(r) => `${r.kind}-${r.id}`}
            dataSource={data.rows}
            scroll={{ x: 900 }}
            pagination={{ pageSize: 50, hideOnSinglePage: true }}
            columns={[
              { title: t('reports.type'), dataIndex: 'kind', render: (k: string) => <Tag color={k === 'Invoice' ? 'blue' : 'orange'}>{t(`reports.${k}`)}</Tag> },
              { title: t('doc.number'), dataIndex: 'number', render: (v: string) => <Ltr>{v}</Ltr> },
              { title: t('doc.date'), dataIndex: 'date', render: formatDate },
              { title: t('doc.client'), dataIndex: 'clientName' },
              { title: t('reports.cashier'), dataIndex: 'cashierName' },
              { title: t('reports.net'), dataIndex: 'net', className: 'num', render: (v: number) => <Money value={v} /> },
              { title: t('reports.vatAmount'), dataIndex: 'vat', className: 'num', render: (v: number) => <Money value={v} /> },
              { title: t('common.total'), dataIndex: 'total', className: 'num', render: (v: number) => <Money value={v} /> },
            ]}
          />
          {isAdmin && data.byCashier.length > 0 && (
            <Card title={t('reports.byCashier')} size="small" style={{ marginTop: 16 }}>
              <Table size="small" rowKey="cashierId" pagination={false} dataSource={data.byCashier} columns={[
                { title: t('reports.cashier'), dataIndex: 'cashierName' },
                { title: t('reports.count'), dataIndex: 'invoiceCount', className: 'num' },
                { title: t('reports.net'), dataIndex: 'net', className: 'num', render: (v: number) => <Money value={v} /> },
                { title: t('common.total'), dataIndex: 'total', className: 'num', render: (v: number) => <Money value={v} /> },
              ]} />
            </Card>
          )}
        </>
      )}
    </>
  );
}

function PurchasesReport() {
  const { t } = useTranslation();
  const [range, setRange] = useRange();
  const [supplierId, setSupplierId] = useState<number | undefined>();
  const { data } = $api.useQuery('get', '/api/reports/purchases', { params: { query: { from: isoDate(range[0]), to: isoDate(range[1]), supplierId } } });
  return (
    <>
      <RangeBar range={range} onChange={setRange} exportPath={`/api/reports/purchases/export?${query(range, { supplierId })}`}>
        <div style={{ width: 240 }}><ContactSelect kind="suppliers" value={supplierId} onChange={setSupplierId} placeholder={t('doc.supplier')} /></div>
        {supplierId && <Button onClick={() => setSupplierId(undefined)}>{t('common.all')}</Button>}
      </RangeBar>
      {data && (
        <>
          <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
            <Col xs={12} md={6}><Card><Statistic title={t('reports.count')} value={data.count} /></Card></Col>
            <Col xs={12} md={6}><Card><Statistic title={t('reports.net')} value={data.net} formatter={(v) => <Ltr>{formatMoney(Number(v))}</Ltr>} /></Card></Col>
            <Col xs={12} md={6}><Card><Statistic title={t('reports.inputVat')} value={data.vat} formatter={(v) => <Ltr>{formatMoney(Number(v))}</Ltr>} /></Card></Col>
            <Col xs={12} md={6}><Card><Statistic title={t('common.total')} value={data.total} formatter={(v) => <Ltr>{formatMoney(Number(v))}</Ltr>} /></Card></Col>
          </Row>
          <Table size="small" rowKey="id" dataSource={data.rows} scroll={{ x: 800 }} pagination={{ pageSize: 50, hideOnSinglePage: true }} columns={[
            { title: t('doc.number'), dataIndex: 'number', render: (v: string) => <Ltr>{v}</Ltr> },
            { title: t('purchase.supplierInvoiceNo'), dataIndex: 'supplierInvoiceNo', render: (v: string) => <Ltr>{v}</Ltr> },
            { title: t('doc.date'), dataIndex: 'date', render: formatDate },
            { title: t('doc.supplier'), dataIndex: 'supplierName' },
            { title: t('reports.net'), dataIndex: 'net', className: 'num', render: (v: number) => <Money value={v} /> },
            { title: t('reports.vatAmount'), dataIndex: 'vat', className: 'num', render: (v: number) => <Money value={v} /> },
            { title: t('common.total'), dataIndex: 'total', className: 'num', render: (v: number) => <Money value={v} /> },
          ]} />
        </>
      )}
    </>
  );
}

function ProfitLossReport() {
  const { t } = useTranslation();
  const [range, setRange] = useRange();
  const { data } = $api.useQuery('get', '/api/reports/profit-loss', { params: { query: { from: isoDate(range[0]), to: isoDate(range[1]) } } });
  return (
    <>
      <RangeBar range={range} onChange={setRange} exportPath={`/api/reports/profit-loss/export?${query(range)}`} />
      {data && (
        <Row gutter={[16, 16]}>
          <Col xs={24} lg={10}>
            <Descriptions bordered column={1} size="small" items={[
              { key: 's', label: t('reports.grossSales'), children: <Money value={data.sales} /> },
              { key: 'c', label: t('reports.lessCredits'), children: <Money value={-data.creditNotes} /> },
              { key: 'n', label: <strong>{t('reports.netSales')}</strong>, children: <Money value={data.netSales} strong /> },
              { key: 'i', label: t('reports.inventory'), children: <Money value={data.inventoryPurchases} /> },
              ...data.expenses.map((e) => ({ key: `e-${e.category}`, label: `${t('reports.expenses')}: ${e.category}`, children: <Money value={e.amount} /> })),
              { key: 't', label: <strong>{t('reports.totalCosts')}</strong>, children: <Money value={data.totalPurchases} strong /> },
              { key: 'p', label: <strong>{t('reports.netProfit')}</strong>, children: <span data-testid="pl-net-profit" style={{ color: data.netProfit < 0 ? '#cf1322' : '#389e0d' }}><Money value={data.netProfit} strong /></span> },
            ]} />
          </Col>
          <Col xs={24} lg={14}>
            <Table size="small" rowKey="month" pagination={false} dataSource={data.monthly} columns={[
              { title: t('reports.month'), dataIndex: 'month', render: (m: string) => <Ltr>{m}</Ltr> },
              { title: t('reports.netSales'), dataIndex: 'sales', className: 'num', render: (v: number) => <Money value={v} /> },
              { title: t('reports.purchases'), dataIndex: 'purchases', className: 'num', render: (v: number) => <Money value={v} /> },
              { title: t('reports.netProfit'), dataIndex: 'profit', className: 'num', render: (v: number) => <Money value={v} /> },
            ]} />
          </Col>
        </Row>
      )}
    </>
  );
}

function VatReport() {
  const { t } = useTranslation();
  const [range, setRange] = useState<Range>([dayjs().startOf('month').subtract(dayjs().month() % 3, 'month'), dayjs()]);
  const { data } = $api.useQuery('get', '/api/reports/vat', { params: { query: { from: isoDate(range[0]), to: isoDate(range[1]) } } });
  return (
    <>
      <RangeBar range={range} onChange={setRange} exportPath={`/api/reports/vat/export?${query(range)}`} />
      {data && (
        <>
          <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
            <Col xs={24} md={8}><Card><Statistic title={t('reports.outputVat')} value={data.outputVat} formatter={(v) => <Ltr>{formatMoney(Number(v))}</Ltr>} /></Card></Col>
            <Col xs={24} md={8}><Card><Statistic title={t('reports.inputVat')} value={data.inputVat} formatter={(v) => <Ltr>{formatMoney(Number(v))}</Ltr>} /></Card></Col>
            <Col xs={24} md={8}>
              <Card>
                <Statistic
                  title={data.netVatPayable >= 0 ? t('reports.payable') : t('reports.refundable')}
                  value={Math.abs(data.netVatPayable)}
                  formatter={(v) => <Ltr>{formatMoney(Number(v))}</Ltr>}
                  styles={{ content: { color: data.netVatPayable >= 0 ? '#cf1322' : '#389e0d' } }}
                  data-testid="vat-net"
                />
              </Card>
            </Col>
          </Row>
          <Alert type="info" showIcon style={{ marginBottom: 16 }} title={`${t('reports.emirate')}: ${t(`settings.${data.emirate}`)}`} />
          <Table size="small" rowKey="box" pagination={false} dataSource={data.boxes} columns={[
            { title: t('reports.box'), dataIndex: 'box', width: 70 },
            { title: t('reports.boxLabel'), dataIndex: 'label' },
            { title: t('reports.net'), dataIndex: 'amount', className: 'num', render: (v: number) => <Money value={v} /> },
            { title: t('reports.vatAmount'), dataIndex: 'vat', className: 'num', render: (v: number) => <Money value={v} /> },
          ]} />
        </>
      )}
    </>
  );
}

export function ReportsPage() {
  const { t } = useTranslation();
  const { isAdmin } = useAuth();
  const items = [
    { key: 'sales', label: t('reports.sales'), children: <SalesReport /> },
    ...(isAdmin
      ? [
          { key: 'purchases', label: t('reports.purchases'), children: <PurchasesReport /> },
          { key: 'pl', label: t('reports.profitLoss'), children: <ProfitLossReport /> },
          { key: 'vat', label: t('reports.vat'), children: <VatReport /> },
        ]
      : []),
  ];
  return (
    <>
      <PageHeader title={t('reports.title')} />
      <Card>
        <Tabs items={items} destroyOnHidden />
      </Card>
    </>
  );
}
