import { DeleteOutlined, EditOutlined, HistoryOutlined, PlusOutlined, SwapOutlined } from '@ant-design/icons';
import { App, Button, Checkbox, Drawer, Form, Input, InputNumber, Modal, Popconfirm, Radio, Select, Switch, Table, Tag } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { TAX_CATEGORIES } from '@fatoura/shared';
import { $api, fetchClient, type Schemas } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { Ltr, Money } from '../../components/Ltr';
import { PageHeader } from '../../components/PageHeader';
import { applyProblem } from '../../components/problems';
import { formatQty } from '../../utils/format';

type Item = Schemas['ItemDto'];

export function ItemsPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [lowStock, setLowStock] = useState(false);
  const [editing, setEditing] = useState<Item | 'new' | null>(null);
  const [adjusting, setAdjusting] = useState<Item | null>(null);
  const [history, setHistory] = useState<Item | null>(null);
  const [form] = Form.useForm();
  const [adjustForm] = Form.useForm();
  const list = $api.useQuery('get', '/api/items', { params: { query: { search: search || undefined, page, pageSize: 20, lowStock: lowStock || undefined, includeInactive: isAdmin || undefined } } });
  const type = Form.useWatch('type', form);

  useEffect(() => {
    if (editing === 'new') form.setFieldsValue({ type: 'Service', name: '', description: '', unitPrice: 0, taxCategory: 'Standard', trackStock: false, reorderLevel: 0, isActive: true });
    else if (editing) form.setFieldsValue(editing);
  }, [editing, form]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['get', '/api/items'] });

  const save = async () => {
    const values = await form.validateFields().catch(() => null);
    if (!values) return;
    const body = { ...values, trackStock: values.type === 'Product' && values.trackStock, reorderLevel: values.reorderLevel ?? 0 };
    const { error } = editing === 'new'
      ? await fetchClient.POST('/api/items', { body })
      : await fetchClient.PUT('/api/items/{id}', { params: { path: { id: (editing as Item).id } }, body });
    if (error) return void message.error(applyProblem(error, form));
    message.success(t('common.saved'));
    setEditing(null);
    await refresh();
  };

  const adjust = async () => {
    const values = await adjustForm.validateFields().catch(() => null);
    if (!values) return;
    const { error } = await fetchClient.POST('/api/items/{id}/adjust-stock', { params: { path: { id: adjusting!.id } }, body: values });
    if (error) return void message.error(applyProblem(error, adjustForm));
    message.success(t('common.saved'));
    setAdjusting(null);
    adjustForm.resetFields();
    await refresh();
  };

  const remove = async (id: number) => {
    const { error } = await fetchClient.DELETE('/api/items/{id}', { params: { path: { id } } });
    if (error) message.error(applyProblem(error));
    else await refresh();
  };

  return (
    <>
      <PageHeader
        title={t('nav.items')}
        extra={
          <>
            <Input.Search allowClear placeholder={t('common.search')} onSearch={(v) => { setSearch(v); setPage(1); }} style={{ width: 240 }} />
            <Checkbox checked={lowStock} onChange={(e) => setLowStock(e.target.checked)}>{t('item.lowStockOnly')}</Checkbox>
            {isAdmin && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')} data-testid="item-new">{t('item.newItem')}</Button>}
          </>
        }
      />
      <Table
        rowKey="id"
        loading={list.isLoading}
        dataSource={list.data?.items}
        scroll={{ x: 900 }}
        pagination={{ current: page, pageSize: 20, total: list.data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: t('item.name'), dataIndex: 'name', render: (v: string, r: Item) => <>{v} {!r.isActive && <Tag>{t('common.inactive')}</Tag>}<div style={{ fontSize: 12, opacity: 0.65 }}>{r.description}</div></> },
          { title: t('item.type'), dataIndex: 'type', render: (v: string) => t(`item.${v}`) },
          { title: t('item.unitPrice'), dataIndex: 'unitPrice', className: 'num', render: (v: number) => <Money value={v} /> },
          { title: t('item.taxCategory'), dataIndex: 'taxCategory', render: (v: string) => <Tag>{t(`item.${v}`)}</Tag> },
          {
            title: t('item.stock'),
            dataIndex: 'stockQty',
            className: 'num',
            render: (v: number, r: Item) => (r.trackStock ? <Tag color={r.isLowStock ? (v <= 0 ? 'red' : 'orange') : 'green'}><Ltr>{formatQty(v)}</Ltr></Tag> : '—'),
          },
          ...(isAdmin
            ? [
                { title: t('item.avgCost'), dataIndex: 'avgCost', className: 'num', render: (v: number, r: Item) => (r.trackStock ? <Money value={v} /> : '—') },
                {
                  title: t('common.actions'),
                  key: 'actions',
                  width: 170,
                  render: (_: unknown, r: Item) => (
                    <>
                      <Button type="text" icon={<EditOutlined />} onClick={() => setEditing(r)} aria-label={t('common.edit')} />
                      {r.trackStock && <Button type="text" icon={<SwapOutlined />} onClick={() => setAdjusting(r)} title={t('item.adjustStock')} />}
                      {r.trackStock && <Button type="text" icon={<HistoryOutlined />} onClick={() => setHistory(r)} title={t('item.movements')} />}
                      <Popconfirm title={t('item.deleteConfirm')} onConfirm={() => remove(r.id)} okText={t('common.yes')} cancelText={t('common.no')}>
                        <Button type="text" danger icon={<DeleteOutlined />} aria-label={t('common.delete')} />
                      </Popconfirm>
                    </>
                  ),
                },
              ]
            : []),
        ]}
      />
      <Drawer open={editing !== null} onClose={() => setEditing(null)} title={editing === 'new' ? t('item.newItem') : t('item.editItem')} size={480} destroyOnHidden
        extra={<Button type="primary" onClick={save} data-testid="item-save">{t('common.save')}</Button>}>
        <Form form={form} layout="vertical">
          <Form.Item name="type" label={t('item.type')}>
            <Radio.Group options={[{ value: 'Product', label: t('item.Product') }, { value: 'Service', label: t('item.Service') }]} optionType="button" />
          </Form.Item>
          <Form.Item name="name" label={t('item.name')} rules={[{ required: true, whitespace: true, message: t('common.required') }]}>
            <Input maxLength={200} data-testid="item-name" />
          </Form.Item>
          <Form.Item name="description" label={t('item.description')}>
            <Input.TextArea maxLength={1000} autoSize={{ minRows: 2 }} />
          </Form.Item>
          <Form.Item name="unitPrice" label={t('item.unitPrice')} rules={[{ required: true, message: t('common.required') }]}>
            <InputNumber min={0} precision={2} style={{ width: '100%' }} data-testid="item-price" />
          </Form.Item>
          <Form.Item name="taxCategory" label={t('item.taxCategory')}>
            <Select options={TAX_CATEGORIES.map((c) => ({ value: c, label: t(`item.${c}`) }))} />
          </Form.Item>
          {type === 'Product' && (
            <>
              <Form.Item name="trackStock" label={t('item.trackStock')} valuePropName="checked">
                <Switch />
              </Form.Item>
              <Form.Item name="reorderLevel" label={t('item.reorderLevel')}>
                <InputNumber min={0} precision={3} style={{ width: '100%' }} />
              </Form.Item>
            </>
          )}
          <Form.Item name="isActive" label={t('common.active')} valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Drawer>
      <Modal open={adjusting !== null} title={`${t('item.adjustStock')} — ${adjusting?.name ?? ''}`} onOk={adjust} onCancel={() => setAdjusting(null)} okText={t('common.save')} cancelText={t('common.cancel')} destroyOnHidden>
        <Form form={adjustForm} layout="vertical">
          <Form.Item name="quantity" label={t('item.adjustQty')} rules={[{ required: true, message: t('common.required') }]}>
            <InputNumber precision={3} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="note" label={t('item.adjustNote')} rules={[{ required: true, whitespace: true, message: t('common.required') }]}>
            <Input maxLength={500} />
          </Form.Item>
        </Form>
      </Modal>
      <StockHistory item={history} onClose={() => setHistory(null)} />
    </>
  );
}

function StockHistory({ item, onClose }: { item: Item | null; onClose: () => void }) {
  const { t } = useTranslation();
  const query = $api.useQuery('get', '/api/items/{id}/movements', { params: { path: { id: item?.id ?? 0 }, query: { pageSize: 100 } } }, { enabled: item !== null });
  return (
    <Drawer open={item !== null} onClose={onClose} title={`${t('item.movements')} — ${item?.name ?? ''}`} size={640}>
      <Table
        size="small"
        rowKey="id"
        loading={query.isLoading}
        dataSource={query.data?.items}
        pagination={false}
        columns={[
          { title: t('doc.date'), dataIndex: 'at', render: (v: string) => new Date(v).toLocaleString() },
          { title: t('item.type'), dataIndex: 'type' },
          { title: t('doc.number'), dataIndex: 'reference', render: (v: string) => <Ltr>{v}</Ltr> },
          { title: t('doc.qty'), dataIndex: 'quantity', className: 'num', render: (v: number) => <Ltr>{v > 0 ? `+${formatQty(v)}` : formatQty(v)}</Ltr> },
          { title: t('item.adjustNote'), dataIndex: 'note' },
        ]}
      />
    </Drawer>
  );
}
