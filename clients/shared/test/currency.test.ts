import { describe, expect, it } from 'vitest';
import { calculateDocument, toAed, type TaxCategory } from '../src/calc';
import { loadSpec } from './spec';

interface CurrencySpec {
  standardRate: string;
  cases: Array<{
    name: string;
    exchangeRate: string;
    lines: Array<{ qty: string; unitPrice: string; tax: TaxCategory }>;
    expected: { subTotal: string; vatTotal: string; total: string; aed: { subTotal: string; vatTotal: string; total: string } };
  }>;
}

const spec = loadSpec<CurrencySpec>('currency-cases.json');

describe('toAed matches spec/currency-cases.json', () => {
  it.each(spec.cases.map((c) => [c.name, c] as const))('%s', (_name, c) => {
    const totals = calculateDocument(c.lines, spec.standardRate);
    expect(totals.total.toFixed(2)).toBe(c.expected.total);
    const aed = toAed(totals, c.exchangeRate);
    expect({ subTotal: aed.subTotal.toFixed(2), vatTotal: aed.vatTotal.toFixed(2), total: aed.total.toFixed(2) }).toEqual({
      subTotal: c.expected.aed.subTotal,
      vatTotal: c.expected.aed.vatTotal,
      total: c.expected.aed.total,
    });
  });
});
