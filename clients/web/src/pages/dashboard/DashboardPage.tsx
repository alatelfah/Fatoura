import { FileAddOutlined, FileTextOutlined, UserAddOutlined, WarningOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Col, Empty, Flex, List, Row, Skeleton, Statistic, Table, Tag, Typography } from 'antd';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { $api, type Schemas } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { Auto, Ltr, Money } from '../../components/Ltr';
import { PageHeader } from '../../components/PageHeader';
import { formatDate, formatMoney, formatQty } from '../../utils/format';

export function DashboardPage() {
  const { isAdmin } = useAuth();
  return isAdmin ? <AdminDashboard /> : <CashierDashboard />;
}

function Kpi({ title, value, suffix, testId, danger }: { title: string; value: number; suffix?: string; testId?: string; danger?: boolean }) {
  return (
    <Card className="kpi">
      <Statistic
        title={title}
        value={value}
        formatter={(v) => <Ltr>{formatMoney(Number(v))}</Ltr>}
        suffix={suffix}
        styles={{ content: danger && value < 0 ? { color: '#cf1322' } : undefined }}
        data-testid={testId}
      />
    </Card>
  );
}

function RecentInvoices({ invoices }: { invoices: Schemas['RecentInvoiceDto'][] }) {
  const { t } = useTranslation();
  return (
    <Table
      size="small"
      rowKey="id"
      pagination={false}
      dataSource={invoices}
      columns={[
        { title: t('doc.number'), dataIndex: 'number', render: (n: string, r) => <Link to={`/invoices/${r.id}`}><Ltr>{n}</Ltr></Link> },
        { title: t('doc.date'), dataIndex: 'date', render: formatDate },
        { title: t('doc.client'), dataIndex: 'clientName', render: (v: string) => <Auto>{v}</Auto> },
        { title: t('common.total'), dataIndex: 'total', className: 'num', render: (v: number, r) => <Money value={v} currency={r.currency} /> },
        {
          title: t('doc.balance'),
          dataIndex: 'balance',
          className: 'num',
          render: (v: number, r) => (r.status === 'Void' ? <Tag color="red">{t('invoice.Void')}</Tag> : <Money value={v} currency={r.currency} />),
        },
      ]}
    />
  );
}

function AdminDashboard() {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const { data, isLoading, error } = $api.useQuery('get', '/api/dashboard/admin');
  const settings = $api.useQuery('get', '/api/settings');
  const rtl = i18n.language === 'ar';

  if (isLoading || !data) return error ? <Alert type="error" title={t('common.error')} /> : <Skeleton active />;
  const chart = data.monthly.map((m) => ({ month: dayjs(`${m.month}-01`).format('MMM YY'), [t('dashboard.sales')]: m.sales, [t('dashboard.purchases')]: m.purchases }));

  return (
    <>
      <PageHeader title={t('dashboard.welcome', { name: user?.displayName })} subtitle={`${t('dashboard.yearToDate')}: ${formatDate(data.period.from)} – ${formatDate(data.period.to)} · ${t('reports.inAed')}`} />
      {settings.data && !settings.data.isComplete && (
        <Alert type="warning" showIcon title={t('settings.incomplete')} action={<Link to="/settings">{t('nav.settings')}</Link>} style={{ marginBottom: 16 }} />
      )}
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} xl={6}><Kpi title={`${t('dashboard.sales')} (${t('dashboard.exclVat')})`} value={data.sales} testId="kpi-sales" /></Col>
        <Col xs={24} sm={12} xl={6}><Kpi title={t('dashboard.purchases')} value={data.purchases} testId="kpi-purchases" /></Col>
        <Col xs={24} sm={12} xl={6}><Kpi title={t('dashboard.netProfit')} value={data.netProfit} danger testId="kpi-profit" /></Col>
        <Col xs={24} sm={12} xl={6}>
          <Card className="kpi">
            <Flex justify="space-between">
              <Statistic title={t('dashboard.openInvoices')} value={data.openInvoiceCount} />
              <Statistic title={t('dashboard.openQuotations')} value={data.openQuotationCount} />
            </Flex>
            <Typography.Text type="secondary">
              {t('dashboard.openBalance')}: <Money value={data.openInvoiceBalance} />
            </Typography.Text>
          </Card>
        </Col>
        <Col xs={24} xl={16}>
          <Card title={t('dashboard.monthly')}>
            <div style={{ width: '100%', height: 300 }} dir="ltr">
              <ResponsiveContainer>
                <BarChart data={chart}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} />
                  <XAxis dataKey="month" reversed={rtl} />
                  <YAxis orientation={rtl ? 'right' : 'left'} tickFormatter={(v: number) => (v >= 1000 ? `${Math.round(v / 1000)}k` : String(v))} width={48} />
                  <Tooltip formatter={(v) => formatMoney(Number(v))} cursor={{ fill: 'rgba(31,58,95,0.06)' }} />
                  <Legend />
                  <Bar dataKey={t('dashboard.sales')} fill="#1f3a5f" radius={[3, 3, 0, 0]} isAnimationActive={false} />
                  <Bar dataKey={t('dashboard.purchases')} fill="#f5a623" radius={[3, 3, 0, 0]} isAnimationActive={false} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </Card>
        </Col>
        <Col xs={24} xl={8}>
          <Card title={<><WarningOutlined style={{ color: '#fa8c16' }} /> {t('dashboard.lowStock')} {data.lowStockCount > 0 && <Tag color="orange">{data.lowStockCount}</Tag>}</>} style={{ height: '100%' }}>
            {data.lowStockItems.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('dashboard.noLowStock')} />
            ) : (
              <List
                size="small"
                dataSource={data.lowStockItems}
                renderItem={(i) => (
                  <List.Item extra={<Tag color={i.stockQty <= 0 ? 'red' : 'orange'}><Ltr>{formatQty(i.stockQty)}</Ltr></Tag>}>
                    <Link to="/items">{i.name}</Link>
                  </List.Item>
                )}
                data-testid="low-stock-list"
              />
            )}
          </Card>
        </Col>
        <Col xs={24} md={12}><Kpi title={t('dashboard.outputVat')} value={data.outputVat} /></Col>
        <Col xs={24} md={12}><Kpi title={t('dashboard.inputVat')} value={data.inputVat} /></Col>
        <Col span={24}>
          <Card title={t('dashboard.recentInvoices')}>
            <RecentInvoices invoices={data.recentInvoices} />
          </Card>
        </Col>
      </Row>
    </>
  );
}

function CashierDashboard() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const { data, isLoading } = $api.useQuery('get', '/api/dashboard/cashier');
  if (isLoading || !data) return <Skeleton active />;
  return (
    <>
      <PageHeader title={t('dashboard.welcome', { name: user?.displayName })} subtitle={formatDate(data.today)} />
      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}><Kpi title={`${t('dashboard.todaySales')} (${t('dashboard.inclVat')})`} value={data.shiftSalesTotal} testId="kpi-shift-total" /></Col>
        <Col xs={24} md={8}><Kpi title={`${t('dashboard.todaySales')} (${t('dashboard.exclVat')})`} value={data.shiftSalesNet} /></Col>
        <Col xs={24} md={8}>
          <Card className="kpi">
            <Statistic title={t('dashboard.shiftInvoices')} value={data.shiftInvoiceCount} data-testid="kpi-shift-count" />
          </Card>
        </Col>
        <Col span={24}>
          <Card title={t('dashboard.quickActions')}>
            <Flex gap={12} wrap>
              <Button type="primary" size="large" icon={<FileAddOutlined />} onClick={() => navigate('/invoices/new')} data-testid="quick-new-invoice">
                {t('dashboard.newInvoice')}
              </Button>
              <Button size="large" icon={<FileTextOutlined />} onClick={() => navigate('/quotations/new')}>
                {t('dashboard.newQuotation')}
              </Button>
              <Button size="large" icon={<UserAddOutlined />} onClick={() => navigate('/clients?new=1')}>
                {t('dashboard.newClient')}
              </Button>
            </Flex>
          </Card>
        </Col>
        <Col span={24}>
          <Card title={t('dashboard.myRecentInvoices')}>
            <RecentInvoices invoices={data.recentInvoices} />
          </Card>
        </Col>
      </Row>
    </>
  );
}
