import { describe, expect, it } from 'vitest';
import {
  allocate,
  calculateDocument,
  calculateLine,
  formatMoney,
  previewDocument,
  type DiscountKind,
  type TaxCategory,
} from '../src/calc';
import { loadSpec } from './spec';

interface CalcSpec {
  standardRate: string;
  cases: Array<{
    name: string;
    lines: Array<{ qty: string; unitPrice: string; tax: TaxCategory }>;
    discount?: { kind: DiscountKind; value: string };
    expected: {
      lines: Array<{ discount?: string; net: string; vat: string; total: string }>;
      grossSubTotal?: string;
      discount?: string;
      subTotal: string;
      vatTotal: string;
      total: string;
    };
  }>;
}

const spec = loadSpec<CalcSpec>('calc-cases.json');

describe('calculateDocument matches spec/calc-cases.json', () => {
  it.each(spec.cases.map((c) => [c.name, c] as const))('%s', (_name, c) => {
    const result = calculateDocument(c.lines, spec.standardRate, c.discount);
    expect(
      result.lines.map((l) => ({
        discount: l.discount.toFixed(2),
        net: l.net.toFixed(2),
        vat: l.vat.toFixed(2),
        total: l.total.toFixed(2),
      })),
    ).toEqual(c.expected.lines.map((l) => ({ discount: '0.00', ...l })));
    expect(result.discount.toFixed(2)).toBe(c.expected.discount ?? '0.00');
    expect(result.grossSubTotal.toFixed(2)).toBe(c.expected.grossSubTotal ?? c.expected.subTotal);
    expect(result.subTotal.toFixed(2)).toBe(c.expected.subTotal);
    expect(result.vatTotal.toFixed(2)).toBe(c.expected.vatTotal);
    expect(result.total.toFixed(2)).toBe(c.expected.total);
  });
});

describe('calculateLine', () => {
  it('rejects non-positive quantity', () => {
    expect(() => calculateLine({ qty: 0, unitPrice: 1, tax: 'Standard' })).toThrow(RangeError);
  });
  it('rejects negative price', () => {
    expect(() => calculateLine({ qty: 1, unitPrice: -1, tax: 'Standard' })).toThrow(RangeError);
  });
  it('does not suffer from float error', () => {
    // 0.1 * 3 in floats is 0.30000000000000004
    expect(calculateLine({ qty: 3, unitPrice: 0.1, tax: 'Standard' }).net.toFixed(2)).toBe('0.30');
  });
});

describe('discounts', () => {
  it.each([
    ['Percent', '100.01'],
    ['Percent', '-1'],
    ['Percent', '10.005'],
    ['Amount', '30.01'],
    ['Amount', '-0.01'],
    ['Amount', '0.001'],
  ] as const)('rejects %s %s', (kind, value) => {
    expect(() => calculateDocument([{ qty: 2, unitPrice: 15, tax: 'Standard' }], '0.05', { kind, value })).toThrow(RangeError);
  });

  it('allocation shares always add up and never exceed a line', () => {
    let seed = 42;
    const next = (n: number) => (seed = (seed * 1103515245 + 12345) % 2147483648) % n;
    for (let run = 0; run < 500; run++) {
      const weights = Array.from({ length: 1 + next(7) }, () => (next(100_000) / 100).toFixed(2));
      const sum = weights.reduce((s, w) => s + Math.round(Number(w) * 100), 0);
      const amount = (next(sum + 1) / 100).toFixed(2);
      const shares = allocate(amount, weights);
      expect(shares.reduce((s, x) => s + Math.round(Number(x) * 100), 0) / 100).toBeCloseTo(Number(amount), 2);
      shares.forEach((s, i) => expect(s.gte(0) && s.lte(weights[i]!)).toBe(true));
    }
  });

  it('preview ignores an invalid discount instead of throwing', () => {
    const r = previewDocument([{ qty: 1, unitPrice: 10, tax: 'Standard' }], '0.05', { kind: 'Amount', value: '50' });
    expect(r.discount.toFixed(2)).toBe('0.00');
    expect(r.total.toFixed(2)).toBe('10.50');
  });

  it('preview spreads the discount over complete lines only', () => {
    const r = previewDocument(
      [
        { qty: 1, unitPrice: 100, tax: 'Standard' },
        { qty: null, unitPrice: 10, tax: 'Standard' },
      ],
      '0.05',
      { kind: 'Percent', value: '10' },
    );
    expect(r.lines[0]!.discount.toFixed(2)).toBe('10.00');
    expect(r.lines[1]!.total.toFixed(2)).toBe('0.00');
    expect(r.total.toFixed(2)).toBe('94.50');
  });
});

describe('previewDocument', () => {
  it('skips incomplete lines but keeps their positions', () => {
    const r = previewDocument([
      { qty: 20, unitPrice: 2050, tax: 'Standard' },
      { qty: null, unitPrice: 10, tax: 'Standard' },
    ]);
    expect(r.lines).toHaveLength(2);
    expect(r.lines[1]!.total.toFixed(2)).toBe('0.00');
    expect(r.total.toFixed(2)).toBe('43050.00');
  });
});

describe('formatMoney', () => {
  it.each([
    [48300, '48,300.00'],
    ['2050', '2,050.00'],
    [0, '0.00'],
    [1234567.895, '1,234,567.90'],
    [-1500.5, '-1,500.50'],
  ])('%s -> %s', (input, expected) => {
    expect(formatMoney(input)).toBe(expected);
  });
});
