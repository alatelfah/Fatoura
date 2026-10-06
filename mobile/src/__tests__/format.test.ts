import { describe, expect, it } from '@jest/globals';
import { formatDate, formatQty, parseNumber } from '../lib/format';
import { toRequests, newLine } from '../components/DocumentEditor';
import { previewDocument } from '@fatoura/shared';

describe('format helpers', () => {
  it('formats dates like the printed documents', () => {
    expect(formatDate('2026-09-01')).toBe('01 Sep 2026');
    expect(formatDate(null)).toBe('—');
  });

  it('formats quantities without trailing zeros', () => {
    expect(formatQty(20)).toBe('20');
    expect(formatQty(1.5)).toBe('1.5');
    expect(formatQty(1.125)).toBe('1.125');
  });

  it('parses Western and Arabic-Indic digits and thousands separators', () => {
    expect(parseNumber('2,050.00')).toBe(2050);
    expect(parseNumber('٢٠٥٠')).toBe(2050);
    expect(parseNumber('')).toBeNull();
    expect(parseNumber('abc')).toBeNull();
  });
});

describe('document lines', () => {
  it('converts editor lines to API requests and previews the reference totals', () => {
    const lines = [
      { ...newLine(), description: ' Dishwasher ', qty: '20', price: '2,050' },
      { ...newLine(), description: 'Installation', qty: '20', price: '250' },
    ];
    const requests = toRequests(lines);
    expect(requests[0]).toMatchObject({ description: 'Dishwasher', quantity: 20, unitPrice: 2050, taxCategory: 'Standard', itemId: null });
    const totals = previewDocument(requests.map((r) => ({ qty: r.quantity, unitPrice: r.unitPrice, tax: r.taxCategory })));
    expect(totals.total.toFixed(2)).toBe('48300.00');
  });
});
