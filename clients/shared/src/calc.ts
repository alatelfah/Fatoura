import Big from 'big.js';

/**
 * Document math mirrored from backend `Fatoura.Domain.Documents.DocumentCalculator` (BRD §5).
 * Line Gross = round2(Qty × Unit Price); Line Net = Line Gross − Line Discount; Line VAT = round2(Line Net × rate);
 * Sub Total = Σ Line Net; Total VAT = Σ Line VAT; Total = Sub Total + Total VAT.
 * A document discount is spread over the lines in proportion to their gross amounts, to the fils.
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

export type DiscountKind = 'None' | 'Amount' | 'Percent';

export interface DocumentDiscount {
  kind: DiscountKind;
  value: Decimalish;
}

export interface LineAmounts {
  /** Share of the document discount; net is after it. */
  discount: Big;
  net: Big;
  vatRate: Big;
  vat: Big;
  total: Big;
}

export interface DocumentTotals {
  lines: LineAmounts[];
  /** Σ round2(Qty × Unit Price), before the discount. */
  grossSubTotal: Big;
  discount: Big;
  /** After the discount: the taxable amount. */
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

export function grossOf(line: LineInput): Big {
  const qty = new Big(line.qty);
  const price = new Big(line.unitPrice);
  if (qty.lte(0)) throw new RangeError('Quantity must be greater than zero.');
  if (price.lt(0)) throw new RangeError('Unit price cannot be negative.');
  return roundMoney(qty.times(price));
}

export function calculateLine(
  line: LineInput,
  standardRate: Decimalish = DEFAULT_VAT_RATE,
  discount: Decimalish = 0,
): LineAmounts {
  const gross = grossOf(line);
  const share = new Big(discount);
  if (share.lt(0) || share.gt(gross)) throw new RangeError('A line discount must be between zero and the line amount.');
  const vatRate = rateFor(line.tax, standardRate);
  const net = gross.minus(share);
  const vat = roundMoney(net.times(vatRate));
  return { discount: share, net, vatRate, vat, total: net.plus(vat) };
}

function hasAtMost2Decimals(value: Big): boolean {
  return value.round(2, Big.roundDown).eq(value);
}

/** The discount in money: a percentage is round2(gross × % / 100); an amount may not exceed the gross. */
export function discountAmount(discount: DocumentDiscount | null | undefined, grossSubTotal: Decimalish): Big {
  if (!discount || discount.kind === 'None') return new Big(0);
  const value = new Big(discount.value);
  const gross = new Big(grossSubTotal);
  if (discount.kind === 'Percent') {
    if (value.lt(0) || value.gt(100) || !hasAtMost2Decimals(value)) {
      throw new RangeError('A discount percentage must be between 0 and 100 with at most 2 decimals.');
    }
    return roundMoney(gross.times(value).div(100));
  }
  if (value.lt(0) || value.gt(gross) || !hasAtMost2Decimals(value)) {
    throw new RangeError('A discount amount must be between 0 and the sub total with at most 2 decimals.');
  }
  return value;
}

/**
 * Splits `amount` over `weights` proportionally, in whole fils, so the shares add up exactly
 * (largest remainder; ties go to the earlier line). Integer arithmetic keeps it identical to the C# version.
 */
export function allocate(amount: Decimalish, weights: Decimalish[]): Big[] {
  const fils = new Big(amount).times(100);
  const w = weights.map((x) => new Big(x).times(100));
  const total = w.reduce((s, x) => s.plus(x), new Big(0));
  if (fils.eq(0) || total.eq(0)) return w.map(() => new Big(0));
  const shares = w.map((x) => fils.times(x).div(total).round(0, Big.roundDown));
  const remainders = w.map((x, i) => fils.times(x).minus(shares[i]!.times(total)));
  const left = Number(fils.minus(shares.reduce((s, x) => s.plus(x), new Big(0))).toFixed(0));
  const order = w.map((_, i) => i).sort((a, b) => remainders[b]!.cmp(remainders[a]!) || a - b);
  for (const i of order.slice(0, left)) shares[i] = shares[i]!.plus(1);
  return shares.map((s) => s.div(100));
}

export function calculateDocument(
  lines: LineInput[],
  standardRate: Decimalish = DEFAULT_VAT_RATE,
  discount?: DocumentDiscount | null,
): DocumentTotals {
  const gross = lines.map(grossOf);
  const grossSubTotal = gross.reduce((s, g) => s.plus(g), new Big(0));
  const amount = discountAmount(discount, grossSubTotal);
  const shares = allocate(amount, gross);
  const results = lines.map((l, i) => calculateLine(l, standardRate, shares[i]));
  const subTotal = results.reduce((s, r) => s.plus(r.net), new Big(0));
  const vatTotal = results.reduce((s, r) => s.plus(r.vat), new Big(0));
  return { lines: results, grossSubTotal, discount: amount, subTotal, vatTotal, total: subTotal.plus(vatTotal) };
}

/**
 * Like calculateDocument, but skips incomplete lines and an invalid discount (for live previews while the user
 * types). Skipped lines keep their positions with zero amounts.
 */
export function previewDocument(
  lines: Array<{ qty?: Decimalish | null; unitPrice?: Decimalish | null; tax?: TaxCategory | null }>,
  standardRate: Decimalish = DEFAULT_VAT_RATE,
  discount?: DocumentDiscount | null,
): DocumentTotals {
  const valid: LineInput[] = [];
  const positions: number[] = [];
  lines.forEach((l, i) => {
    const line: LineInput = { qty: l.qty ?? 0, unitPrice: l.unitPrice ?? 0, tax: l.tax ?? 'Standard' };
    try {
      grossOf(line);
      valid.push(line);
      positions.push(i);
    } catch {
      // incomplete line
    }
  });
  let totals: DocumentTotals;
  try {
    totals = calculateDocument(valid, standardRate, discount);
  } catch {
    totals = calculateDocument(valid, standardRate);
  }
  const zero = new Big(0);
  const results: LineAmounts[] = lines.map((l) => ({
    discount: zero,
    net: zero,
    vatRate: rateFor(l.tax ?? 'Standard', standardRate),
    vat: zero,
    total: zero,
  }));
  positions.forEach((p, i) => (results[p] = totals.lines[i]!));
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
