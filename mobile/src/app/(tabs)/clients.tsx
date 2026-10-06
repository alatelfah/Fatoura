import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FlatList, View } from 'react-native';
import { Button, Dialog, FAB, HelperText, List, Portal, Searchbar, TextInput } from 'react-native-paper';
import { isValidTrn, normalizeTrn } from '@fatoura/shared';
import { $api, fetchClient } from '../../lib/api';
import { ErrorText } from '../../components/ui';

export default function ClientsScreen() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ name: '', phone: '', email: '', address: '', trn: '' });
  const [error, setError] = useState<unknown>(null);
  const { data, refetch, isRefetching } = $api.useQuery('get', '/api/clients', { params: { query: { search: search || undefined, pageSize: 50 } } });
  const trnInvalid = form.trn.length > 0 && !isValidTrn(form.trn);

  const save = async () => {
    const { error: e } = await fetchClient.POST('/api/clients', { body: { ...form, trn: normalizeTrn(form.trn), isActive: true } });
    if (e) return setError(e);
    setOpen(false);
    setForm({ name: '', phone: '', email: '', address: '', trn: '' });
    await queryClient.invalidateQueries({ queryKey: ['get', '/api/clients'] });
  };

  return (
    <View style={{ flex: 1 }}>
      <Searchbar placeholder={t('common.search')} value={search} onChangeText={setSearch} style={{ margin: 12 }} />
      <FlatList
        data={data?.items ?? []}
        keyExtractor={(c) => String(c.id)}
        refreshing={isRefetching}
        onRefresh={refetch}
        renderItem={({ item }) => (
          <List.Item title={item.name} description={[item.phone, item.trn && `TRN ${item.trn}`, item.address].filter(Boolean).join(' · ')} left={(p) => <List.Icon {...p} icon="account" />} />
        )}
      />
      <FAB icon="account-plus" style={{ position: 'absolute', end: 16, bottom: 16 }} onPress={() => setOpen(true)} testID="client-new" />
      <Portal>
        <Dialog visible={open} onDismiss={() => setOpen(false)}>
          <Dialog.Title>{t('contact.newClient')}</Dialog.Title>
          <Dialog.ScrollArea>
            <View style={{ gap: 8, paddingVertical: 8 }}>
              <TextInput label={t('contact.clientName')} value={form.name} onChangeText={(name) => setForm({ ...form, name })} mode="outlined" />
              <TextInput label={t('contact.phone')} value={form.phone} onChangeText={(phone) => setForm({ ...form, phone })} mode="outlined" keyboardType="phone-pad" />
              <TextInput label={t('contact.email')} value={form.email} onChangeText={(email) => setForm({ ...form, email })} mode="outlined" keyboardType="email-address" autoCapitalize="none" />
              <TextInput label={t('contact.address')} value={form.address} onChangeText={(address) => setForm({ ...form, address })} mode="outlined" multiline />
              <TextInput label={t('contact.trn')} value={form.trn} onChangeText={(trn) => setForm({ ...form, trn })} mode="outlined" keyboardType="number-pad" error={trnInvalid} />
              <HelperText type={trnInvalid ? 'error' : 'info'}>{trnInvalid ? t('contact.trnInvalid') : t('contact.trnHelp')}</HelperText>
              <ErrorText error={error} />
            </View>
          </Dialog.ScrollArea>
          <Dialog.Actions>
            <Button onPress={() => setOpen(false)}>{t('common.cancel')}</Button>
            <Button onPress={save} disabled={!form.name.trim() || trnInvalid}>{t('common.save')}</Button>
          </Dialog.Actions>
        </Dialog>
      </Portal>
    </View>
  );
}
