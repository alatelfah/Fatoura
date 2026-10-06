import { formatMoney } from '@fatoura/shared';

export { formatMoney };

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** "2026-09-01" → "01 Sep 2026" (Latin digits, as on the printed documents). */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const [y, m, d] = iso.slice(0, 10).split('-');
  return `${d} ${MONTHS[Number(m) - 1] ?? ''} ${y}`;
}

export function formatQty(value: number): string {
  return Number.isInteger(value) ? String(value) : String(Number(value.toFixed(3)));
}

/** Parses user input like "1,250.5" or "١٢٣" into a number (or null when empty/invalid). */
export function parseNumber(text: string): number | null {
  const western = text.replace(/[٠-٩]/g, (d) => String('٠١٢٣٤٥٦٧٨٩'.indexOf(d))).replace(/,/g, '').trim();
  if (western === '') return null;
  const n = Number(western);
  return Number.isFinite(n) ? n : null;
}
