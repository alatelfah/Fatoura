import { describe, expect, it } from 'vitest';
import { isValidTrn, normalizeTrn } from '../src/trn';
import { loadSpec } from './spec';

const spec = loadSpec<{ valid: string[]; invalid: string[] }>('trn-cases.json');

describe('isValidTrn matches spec/trn-cases.json', () => {
  it.each(spec.valid)('accepts %s', (trn) => expect(isValidTrn(trn)).toBe(true));
  it.each(spec.invalid)('rejects %s', (trn) => expect(isValidTrn(trn)).toBe(false));
  it('rejects null', () => expect(isValidTrn(null)).toBe(false));
  it('normalizes', () => expect(normalizeTrn(' 100-1234 5678-9012 ')).toBe('100123456789012'));
});
