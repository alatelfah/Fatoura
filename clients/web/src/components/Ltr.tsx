import type { ReactNode } from 'react';
import { formatMoney } from '../utils/format';

/** Keeps numbers, TRNs and document numbers left-to-right inside Arabic text. */
export function Ltr({ children }: { children: ReactNode }) {
  return <bdi dir="ltr">{children}</bdi>;
}

export function Money({ value, strong }: { value: number | string | null | undefined; strong?: boolean }) {
  const text = <Ltr>{formatMoney(value ?? 0)}</Ltr>;
  return strong ? <strong>{text}</strong> : text;
}
