import { DeleteOutlined, UploadOutlined } from '@ant-design/icons';
import { Alert, App, Button, Card, Col, Form, Image, Input, InputNumber, Row, Select, Skeleton, Space, Switch, Table, Tabs, Upload } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatNumber, isValidTrn, normalizeTrn, validatePattern } from '@fatoura/shared';
import { $api, fetchBlob, fetchClient, uploadFile, type Schemas } from '../../api/client';
import { Ltr } from '../../components/Ltr';
import { PageHeader } from '../../components/PageHeader';
import { applyProblem } from '../../components/problems';

const EMIRATES: Schemas['Emirate'][] = ['AbuDhabi', 'Dubai', 'Sharjah', 'Ajman', 'UmmAlQuwain', 'RasAlKhaimah', 'Fujairah'];

export function SettingsPage() {
  const { t } = useTranslation();
  return (
    <>
      <PageHeader title={t('settings.title')} />
      <Card>
        <Tabs
          items={[
            { key: 'company', label: t('settings.company'), children: <CompanySettings /> },
            { key: 'numbering', label: t('settings.numbering'), children: <NumberingSettings /> },
          ]}
        />
      </Card>
    </>
  );
}

function CompanySettings() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [form] = Form.useForm();
  const { data, refetch } = $api.useQuery('get', '/api/settings');

  useEffect(() => {
    if (data) form.setFieldsValue({ ...data, vatRate: Number((data.vatRate * 100).toFixed(2)) });
  }, [data, form]);

  if (!data) return <Skeleton active />;

  const save = async () => {
    const v = await form.validateFields().catch(() => null);
    if (!v) return;
    const { error } = await fetchClient.PUT('/api/settings', { body: { ...v, trn: normalizeTrn(v.trn), vatRate: Number(v.vatRate) / 100 } });
    if (error) return void message.error(applyProblem(error, form));
    message.success(t('common.saved'));
    await refetch();
    await queryClient.invalidateQueries({ queryKey: ['get', '/api/dashboard/admin'] });
  };

  return (
    <Form form={form} layout="vertical" onFinish={save}>
      {!data.isComplete && <Alert type="warning" showIcon title={t('settings.incomplete')} style={{ marginBottom: 16 }} />}
      <Row gutter={16}>
        <Col xs={24} md={12}>
          <Form.Item name="name" label={t('settings.companyName')} rules={[{ required: true, whitespace: true, message: t('common.required') }]}>
            <Input maxLength={200} data-testid="settings-name" />
          </Form.Item>
        </Col>
        <Col xs={24} md={12}>
          <Form.Item name="trn" label={t('contact.trn')} extra={t('contact.trnHelp')}
            rules={[{ required: true, message: t('common.required') }, { validator: async (_, v?: string) => { if (v && !isValidTrn(v)) throw new Error(t('contact.trnInvalid')); } }]}>
            <Input maxLength={20} dir="ltr" data-testid="settings-trn" />
          </Form.Item>
        </Col>
        <Col xs={24} md={12}>
          <Form.Item name="address" label={t('contact.address')} rules={[{ required: true, whitespace: true, message: t('common.required') }]}>
            <Input.TextArea maxLength={500} autoSize data-testid="settings-address" />
          </Form.Item>
        </Col>
        <Col xs={24} md={12}>
          <Form.Item name="emirate" label={t('settings.emirate')}>
            <Select options={EMIRATES.map((e) => ({ value: e, label: t(`settings.${e}`) }))} />
          </Form.Item>
        </Col>
        <Col xs={24} md={8}><Form.Item name="phone" label={t('contact.phone')}><Input maxLength={50} dir="ltr" /></Form.Item></Col>
        <Col xs={24} md={8}><Form.Item name="email" label={t('contact.email')} rules={[{ type: 'email', message: t('common.invalidEmail') }]}><Input maxLength={256} dir="ltr" /></Form.Item></Col>
        <Col xs={24} md={8}><Form.Item name="website" label={t('settings.website')}><Input maxLength={200} dir="ltr" /></Form.Item></Col>
      </Row>
      <Row gutter={16}>
        <Col xs={24} md={12}><ImageSetting kind="logo" has={data.hasLogo} onChange={refetch} /></Col>
        <Col xs={24} md={12}><ImageSetting kind="stamp" has={data.hasStamp} onChange={refetch} /></Col>
      </Row>
      <Card size="small" title={t('settings.documents')} style={{ marginTop: 16 }}>
        <Row gutter={16}>
          <Col xs={12} md={6}>
            <Form.Item name="vatRate" label={t('settings.vatRate')} rules={[{ required: true, message: t('common.required') }]}>
              <InputNumber min={0} max={100} precision={2} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
          <Col xs={12} md={6}>
            <Form.Item name="quotationValidityDays" label={t('settings.quotationValidity')}>
              <InputNumber min={1} max={365} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="allowNegativeStock" label={t('settings.allowNegativeStock')} valuePropName="checked">
              <Switch />
            </Form.Item>
          </Col>
        </Row>
        <Form.Item name="paymentTerms" label={t('doc.paymentTerms')}><Input.TextArea maxLength={1000} autoSize /></Form.Item>
        <Form.Item name="completionOfWork" label={t('doc.completionOfWork')}><Input maxLength={1000} /></Form.Item>
        <Form.Item name="notes" label={t('doc.notes')}><Input.TextArea maxLength={4000} autoSize={{ minRows: 3 }} /></Form.Item>
        <Form.Item name="closingText" label={t('doc.closingText')}><Input.TextArea maxLength={1000} autoSize /></Form.Item>
      </Card>
      <Button type="primary" htmlType="submit" size="large" style={{ marginTop: 16 }} data-testid="settings-save">{t('common.save')}</Button>
    </Form>
  );
}

function ImageSetting({ kind, has, onChange }: { kind: 'logo' | 'stamp'; has: boolean; onChange: () => unknown }) {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const [src, setSrc] = useState<string | null>(null);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let url: string | null = null;
    if (!has) {
      setSrc(null);
      return;
    }
    fetchBlob(`/api/settings/${kind}`).then(({ blob }) => {
      url = URL.createObjectURL(blob);
      setSrc(url);
    }).catch(() => setSrc(null));
    return () => {
      if (url) URL.revokeObjectURL(url);
    };
  }, [has, kind, version]);

  const upload = async (file: File) => {
    const error = await uploadFile(`/api/settings/${kind}`, file);
    if (error) return void message.error(applyProblem(error));
    setVersion((v) => v + 1);
    await onChange();
  };
  const remove = async () => {
    await fetchClient.DELETE(kind === 'logo' ? '/api/settings/logo' : '/api/settings/stamp');
    setVersion((v) => v + 1);
    await onChange();
  };

  return (
    <Card size="small" title={t(`settings.${kind}`)}>
      <Space orientation="vertical">
        {src ? <Image src={src} alt={kind} style={{ maxHeight: 110, maxWidth: 260, objectFit: 'contain' }} /> : <div style={{ height: 60, opacity: 0.5 }}>—</div>}
        <Space>
          <Upload showUploadList={false} accept=".png,.jpg,.jpeg" beforeUpload={(f) => { void upload(f); return false; }}>
            <Button icon={<UploadOutlined />}>{t('settings.uploadImage')}</Button>
          </Upload>
          {has && <Button danger icon={<DeleteOutlined />} onClick={remove}>{t('settings.remove')}</Button>}
        </Space>
      </Space>
    </Card>
  );
}

function NumberingSettings() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const { data, refetch } = $api.useQuery('get', '/api/settings/numbering');
  const [rows, setRows] = useState<Schemas['NumberingDto'][]>([]);
  useEffect(() => {
    if (data) setRows(data);
  }, [data]);
  if (!data) return <Skeleton active />;

  const update = (type: Schemas['DocumentType'], patch: Partial<Schemas['NumberingDto']>) =>
    setRows((r) => r.map((x) => (x.documentType === type ? { ...x, ...patch } : x)));
  const today = new Date().toISOString().slice(0, 10);
  const save = async () => {
    const { error } = await fetchClient.PUT('/api/settings/numbering', { body: rows.map((r) => ({ documentType: r.documentType, pattern: r.pattern, reset: r.reset })) });
    if (error) return void message.error(applyProblem(error));
    message.success(t('common.saved'));
    await refetch();
  };

  return (
    <>
      <Alert type="info" showIcon title={t('settings.patternHelp')} style={{ marginBottom: 16 }} />
      <Table
        rowKey="documentType"
        pagination={false}
        dataSource={rows}
        columns={[
          { title: t('item.type'), dataIndex: 'documentType', render: (v: string) => t(`settings.${v}`) },
          {
            title: t('settings.pattern'),
            dataIndex: 'pattern',
            render: (v: string, r) => {
              const errors = validatePattern(v, r.reset);
              return (
                <Form.Item validateStatus={errors.length ? 'error' : undefined} help={errors[0]} style={{ marginBottom: 0 }}>
                  <Input value={v} dir="ltr" maxLength={40} onChange={(e) => update(r.documentType, { pattern: e.target.value })} />
                </Form.Item>
              );
            },
          },
          {
            title: t('settings.reset'),
            dataIndex: 'reset',
            render: (v: Schemas['SequenceReset'], r) => (
              <Select value={v} style={{ width: 150 }} onChange={(reset) => update(r.documentType, { reset })}
                options={(['Never', 'Yearly', 'Monthly'] as const).map((x) => ({ value: x, label: t(`settings.${x}`) }))} />
            ),
          },
          {
            title: t('settings.example'),
            key: 'example',
            render: (_: unknown, r) => (validatePattern(r.pattern, r.reset).length ? '—' : <Ltr>{formatNumber(r.pattern, today, 1)}</Ltr>),
          },
        ]}
      />
      <Button type="primary" style={{ marginTop: 16 }} onClick={save} disabled={rows.some((r) => validatePattern(r.pattern, r.reset).length > 0)}>
        {t('common.save')}
      </Button>
    </>
  );
}
