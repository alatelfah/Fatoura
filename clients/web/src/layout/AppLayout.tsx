import {
  AppstoreOutlined,
  AuditOutlined,
  BarChartOutlined,
  DashboardOutlined,
  FileDoneOutlined,
  FileTextOutlined,
  LogoutOutlined,
  RollbackOutlined,
  SettingOutlined,
  ShoppingCartOutlined,
  TeamOutlined,
  UserOutlined,
  ShopOutlined,
  KeyOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
} from '@ant-design/icons';
import { Avatar, Button, Dropdown, Flex, Layout, Menu, Tag, Typography, type MenuProps } from 'antd';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import { ChangePasswordModal } from './ChangePasswordModal';
import { LanguageSwitch } from './LanguageSwitch';

const { Header, Sider, Content } = Layout;

export function AppLayout() {
  const { t } = useTranslation();
  const { user, isAdmin, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [collapsed, setCollapsed] = useState(false);
  const [passwordOpen, setPasswordOpen] = useState(false);

  const items: MenuProps['items'] = [
    { key: '/dashboard', icon: <DashboardOutlined />, label: t('nav.dashboard') },
    {
      key: 'sales',
      icon: <FileTextOutlined />,
      label: t('nav.sales'),
      children: [
        { key: '/quotations', icon: <AuditOutlined />, label: t('nav.quotations') },
        { key: '/invoices', icon: <FileDoneOutlined />, label: t('nav.invoices') },
        { key: '/credit-notes', icon: <RollbackOutlined />, label: t('nav.creditNotes') },
      ],
    },
    {
      key: 'contacts',
      icon: <TeamOutlined />,
      label: t('nav.contacts'),
      children: [
        { key: '/clients', icon: <UserOutlined />, label: t('nav.clients') },
        ...(isAdmin ? [{ key: '/suppliers', icon: <ShopOutlined />, label: t('nav.suppliers') }] : []),
      ],
    },
    { key: '/items', icon: <AppstoreOutlined />, label: t('nav.items') },
    ...(isAdmin ? [{ key: '/purchases', icon: <ShoppingCartOutlined />, label: t('nav.purchases') }] : []),
    { key: '/reports', icon: <BarChartOutlined />, label: t('nav.reports') },
    ...(isAdmin
      ? [
          { key: '/settings', icon: <SettingOutlined />, label: t('nav.settings') },
          { key: '/users', icon: <TeamOutlined />, label: t('nav.users') },
        ]
      : []),
  ];

  const selected = '/' + (location.pathname.split('/')[1] ?? '');
  const userMenu: MenuProps['items'] = [
    { key: 'password', icon: <KeyOutlined />, label: t('nav.changePassword'), onClick: () => setPasswordOpen(true) },
    { type: 'divider' },
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: t('nav.logout'),
      danger: true,
      onClick: async () => {
        await logout();
        navigate('/login');
      },
    },
  ];

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider breakpoint="lg" collapsedWidth={0} trigger={null} collapsible collapsed={collapsed} onBreakpoint={setCollapsed} width={232} style={{ background: '#1f3a5f' }}>
        <div className="brand">
          <img src="/favicon.svg" alt="" />
          {!collapsed && <span>{t('app.name')}</span>}
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[selected]}
          defaultOpenKeys={['sales', 'contacts']}
          items={items}
          onClick={(e) => {
            navigate(e.key);
            if (window.innerWidth < 992) setCollapsed(true);
          }}
          style={{ background: 'transparent' }}
          data-testid="main-menu"
        />
      </Sider>
      <Layout>
        <Header style={{ background: '#fff', paddingInline: 16, borderBottom: '1px solid #f0f0f0' }}>
          <Flex justify="space-between" align="center" gap={12} style={{ height: '100%' }}>
            <Button type="text" icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />} onClick={() => setCollapsed((c) => !c)} aria-label="menu" data-testid="menu-toggle" />
            <Flex align="center" gap={12}>
            <LanguageSwitch />
            <Dropdown menu={{ items: userMenu }} trigger={['click']}>
              <Flex align="center" gap={8} style={{ cursor: 'pointer' }} data-testid="user-menu">
                <Avatar icon={<UserOutlined />} style={{ background: '#1f3a5f' }} />
                <Typography.Text>{user?.displayName}</Typography.Text>
                <Tag color={isAdmin ? 'gold' : 'blue'}>{t(`roles.${user?.role ?? 'Cashier'}`)}</Tag>
              </Flex>
            </Dropdown>
            </Flex>
          </Flex>
        </Header>
        <Content style={{ padding: 16, maxWidth: 1400, width: '100%', marginInline: 'auto' }}>
          <Outlet />
        </Content>
      </Layout>
      <ChangePasswordModal open={passwordOpen} onClose={() => setPasswordOpen(false)} />
    </Layout>
  );
}
