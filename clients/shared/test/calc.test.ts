import { describe, expect, it } from 'vitest';
import { calculateDocument, calculateLine, formatMoney, previewDocument, type TaxCategory } from '../src/calc';
import { loadSpec } from './spec';

interface CalcSpec {
  standardRate: string;
  cases: Array<{
    name: string;
    lines: Array<{ qty: string; unitPrice: string; tax: TaxCategory }>;
    expected: {
      lines: Array<{ net: string; vat: string; total: string }>;
      subTotal: string;
      vatTotal: string;
      total: string;
    };
  }>;
}

const spec = loadSpec<CalcSpec>('calc-cases.json');

describe('calculateDocument matches spec/calc-cases.json', () => {
  it.each(spec.cases.map((c) => [c.name, c] as const))('%s', (_name, c) => {
    const result = calculateDocument(c.lines, spec.standardRate);
    expect(result.lines.map((l) => ({ net: l.net.toFixed(2), vat: l.vat.toFixed(2), total: l.total.toFixed(2) }))).toEqual(
      c.expected.lines,
    );
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
