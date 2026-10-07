import type { ReactNode } from 'react';
import { formatMoney } from '../utils/format';

/** Keeps numbers, TRNs and document numbers left-to-right inside Arabic text. */
export function Ltr({ children }: { children: ReactNode }) {
  return <bdi dir="ltr">{children}</bdi>;
}

/** An amount; a currency other than AED is shown after it (lists mix currencies). */
export function Money({ value, strong, currency }: { value: number | string | null | undefined; strong?: boolean; currency?: string }) {
  const text = <Ltr>{formatMoney(value ?? 0)}{currency && currency !== 'AED' ? ` ${currency}` : ''}</Ltr>;
  return strong ? <strong>{text}</strong> : text;
}
