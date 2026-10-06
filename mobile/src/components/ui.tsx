import type { ReactNode } from 'react';
import { RefreshControl, ScrollView, StyleSheet, View, type ViewStyle } from 'react-native';
import { ActivityIndicator, Card, Chip, HelperText, Text } from 'react-native-paper';
import { readProblem } from '@fatoura/shared';
import { formatMoney } from '../lib/format';

/** Numbers, TRNs and document numbers stay left-to-right inside Arabic text. */
export function Ltr({ children, style, bold }: { children: ReactNode; style?: object; bold?: boolean }) {
  return <Text style={[{ writingDirection: 'ltr' }, bold && { fontWeight: '700' }, style]}>{children}</Text>;
}

export function Money({ value, bold, testID }: { value: number | string | null | undefined; bold?: boolean; testID?: string }) {
  return (
    <Text style={[{ writingDirection: 'ltr', fontVariant: ['tabular-nums'] }, bold && { fontWeight: '700' }]} testID={testID}>
      {formatMoney(value ?? 0)}
    </Text>
  );
}

export function Screen({ children, refreshing, onRefresh, style }: { children: ReactNode; refreshing?: boolean; onRefresh?: () => void; style?: ViewStyle }) {
  return (
    <ScrollView
      contentContainerStyle={[styles.screen, style]}
      refreshControl={onRefresh ? <RefreshControl refreshing={!!refreshing} onRefresh={onRefresh} /> : undefined}
      keyboardShouldPersistTaps="handled"
    >
      {children}
    </ScrollView>
  );
}

export function Loading() {
  return (
    <View style={{ padding: 32 }}>
      <ActivityIndicator />
    </View>
  );
}

export function ErrorText({ error }: { error: unknown }) {
  if (!error) return null;
  const { message, fields } = readProblem(error);
  const details = Object.values(fields);
  return (
    <HelperText type="error" visible>
      {details.length ? details.join('\n') : message}
    </HelperText>
  );
}

export function Kpi({ title, value, testID }: { title: string; value: number; testID?: string }) {
  return (
    <Card style={styles.kpi}>
      <Card.Content>
        <Text variant="labelMedium" style={{ opacity: 0.7 }}>{title}</Text>
        <Text variant="titleLarge" style={{ writingDirection: 'ltr', fontWeight: '600' }} testID={testID}>
          {formatMoney(value)}
        </Text>
      </Card.Content>
    </Card>
  );
}

export function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <View style={styles.row}>
      <Text style={{ opacity: 0.7 }}>{label}</Text>
      <View style={{ flexShrink: 1, alignItems: 'flex-end' }}>{typeof children === 'string' ? <Text>{children}</Text> : children}</View>
    </View>
  );
}

export function StatusChip({ label, color }: { label: string; color: string }) {
  return (
    <Chip compact style={{ backgroundColor: color, alignSelf: 'flex-start' }} textStyle={{ color: '#fff', fontSize: 12 }}>
      {label}
    </Chip>
  );
}

export const styles = StyleSheet.create({
  screen: { padding: 16, gap: 12 },
  kpi: { flexGrow: 1, flexBasis: '45%' },
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingVertical: 4, gap: 12 },
});
