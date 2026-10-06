import { Redirect } from 'expo-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { KeyboardAvoidingView, Platform, StyleSheet, View } from 'react-native';
import { Button, HelperText, Surface, Text, TextInput } from 'react-native-paper';
import { LanguageToggle } from '../components/LanguageToggle';
import { useAuth } from '../lib/auth';
import { theme } from '../lib/theme';

export default function LoginScreen() {
  const { t } = useTranslation();
  const { user, login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (user) return <Redirect href="/" />;

  const submit = async () => {
    setBusy(true);
    setError(null);
    const result = await login(email.trim(), password);
    setBusy(false);
    if (result !== 'ok') {
      setError(result === 'offline' ? 'Cannot reach the server.' : result === 'locked' ? 'Account temporarily locked.' : result === 'disabled' ? 'This account is disabled.' : t('auth.invalid'));
    }
  };

  return (
    <KeyboardAvoidingView style={styles.page} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <Surface style={styles.card} elevation={2}>
        <Text variant="headlineMedium" style={styles.title}>{t('app.name')}</Text>
        <Text variant="bodyMedium" style={styles.subtitle}>{t('app.tagline')}</Text>
        <TextInput label={t('auth.email')} value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" autoComplete="email" mode="outlined" testID="login-email" />
        <TextInput label={t('auth.password')} value={password} onChangeText={setPassword} secureTextEntry mode="outlined" style={{ marginTop: 12 }} testID="login-password" onSubmitEditing={submit} />
        {error && <HelperText type="error" visible testID="login-error">{error}</HelperText>}
        <Button mode="contained" onPress={submit} loading={busy} disabled={busy || !email || !password} style={{ marginTop: 16 }} testID="login-submit">
          {t('auth.signIn')}
        </Button>
        <View style={{ alignItems: 'center', marginTop: 16 }}>
          <LanguageToggle />
        </View>
      </Surface>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, justifyContent: 'center', padding: 20, backgroundColor: theme.colors.primary },
  card: { padding: 24, borderRadius: 12, backgroundColor: '#fff', maxWidth: 420, width: '100%', alignSelf: 'center' },
  title: { textAlign: 'center', fontWeight: '700' },
  subtitle: { textAlign: 'center', opacity: 0.7, marginBottom: 20 },
});
