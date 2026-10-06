import Big from 'big.js';

/**
 * Document math mirrored from backend `Fatoura.Domain.Documents.DocumentCalculator` (BRD §5).
 * Line Net = round2(Qty × Unit Price); Line VAT = round2(Line Net × rate);
 * Sub Total = Σ Line Net; Total VAT = Σ Line VAT; Total = Sub Total + Total VAT.
 * Rounding is half away from zero (big.js roundHalfUp). Never use JS floats for money.
 * Verified against spec/calc-cases.json, the same fixture the backend tests use.
 */

export type TaxCategory = 'Standard' | 'ZeroRated' | 'Exempt';

export const TAX_CATEGORIES: readonly TaxCategory[] = ['Standard', 'ZeroRated', 'Exempt'];

export type Decimalish = number | string | Big;

export interface LineInput {
  qty: Decimalish;
  unitPrice: Decimalish;
  tax: TaxCategory;
}

export interface LineAmounts {
  net: Big;
  vatRate: Big;
  vat: Big;
  total: Big;
}

export interface DocumentTotals {
  lines: LineAmounts[];
  subTotal: Big;
  vatTotal: Big;
  total: Big;
}

export const DEFAULT_VAT_RATE = '0.05';

export function roundMoney(value: Big): Big {
  return value.round(2, Big.roundHalfUp);
}

export function rateFor(tax: TaxCategory, standardRate: Decimalish = DEFAULT_VAT_RATE): Big {
  return tax === 'Standard' ? new Big(standardRate) : new Big(0);
}

export function calculateLine(line: LineInput, standardRate: Decimalish = DEFAULT_VAT_RATE): LineAmounts {
  const qty = new Big(line.qty);
  const price = new Big(line.unitPrice);
  if (qty.lte(0)) throw new RangeError('Quantity must be greater than zero.');
  if (price.lt(0)) throw new RangeError('Unit price cannot be negative.');
  const vatRate = rateFor(line.tax, standardRate);
  const net = roundMoney(qty.times(price));
  const vat = roundMoney(net.times(vatRate));
  return { net, vatRate, vat, total: net.plus(vat) };
}

export function calculateDocument(lines: LineInput[], standardRate: Decimalish = DEFAULT_VAT_RATE): DocumentTotals {
  const results = lines.map((l) => calculateLine(l, standardRate));
  const subTotal = results.reduce((s, r) => s.plus(r.net), new Big(0));
  const vatTotal = results.reduce((s, r) => s.plus(r.vat), new Big(0));
  return { lines: results, subTotal, vatTotal, total: subTotal.plus(vatTotal) };
}

/** Like calculateDocument, but skips incomplete lines (for live previews while the user types). */
export function previewDocument(
  lines: Array<{ qty?: Decimalish | null; unitPrice?: Decimalish | null; tax?: TaxCategory | null }>,
  standardRate: Decimalish = DEFAULT_VAT_RATE,
): DocumentTotals {
  const valid: LineInput[] = [];
  const results: LineAmounts[] = [];
  for (const l of lines) {
    try {
      const line: LineInput = { qty: l.qty ?? 0, unitPrice: l.unitPrice ?? 0, tax: l.tax ?? 'Standard' };
      results.push(calculateLine(line, standardRate));
      valid.push(line);
    } catch {
      const zero = new Big(0);
      results.push({ net: zero, vatRate: rateFor(l.tax ?? 'Standard', standardRate), vat: zero, total: zero });
    }
  }
  const totals = calculateDocument(valid, standardRate);
  return { ...totals, lines: results };
}

/** Formats an amount as the invoice does: thousands separators and 2 decimals, Latin digits. */
export function formatMoney(value: Decimalish): string {
  const fixed = roundMoney(new Big(value)).toFixed(2);
  const negative = fixed.startsWith('-');
  const [intPart = '0', frac = '00'] = (negative ? fixed.slice(1) : fixed).split('.');
  const grouped = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  return `${negative ? '-' : ''}${grouped}.${frac}`;
}

export function toNumber(value: Big): number {
  return Number(value.toFixed(2));
}
