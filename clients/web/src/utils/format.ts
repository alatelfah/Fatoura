import dayjs from 'dayjs';
import { formatMoney } from '@fatoura/shared';

export { formatMoney };

export function formatDate(value: string | null | undefined): string {
  return value ? dayjs(value).format('DD MMM YYYY') : '—';
}

export function formatQty(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(3).replace(/0+$/, '').replace(/\.$/, '');
}

/** ISO date (yyyy-mm-dd) for API query strings. */
export function isoDate(value: dayjs.Dayjs | null | undefined): string | undefined {
  return value ? value.format('YYYY-MM-DD') : undefined;
}
