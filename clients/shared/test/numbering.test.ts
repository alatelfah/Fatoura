import { describe, expect, it } from 'vitest';
import { formatNumber, resetKey, validatePattern, type SequenceReset } from '../src/numbering';
import { loadSpec } from './spec';

interface NumberingSpec {
  format: Array<{ pattern: string; date: string; seq: number; expected: string }>;
  resetKey: Array<{ reset: SequenceReset; date: string; expected: string }>;
  invalid: Array<{ pattern: string; reset: SequenceReset }>;
  valid: Array<{ pattern: string; reset: SequenceReset }>;
}

const spec = loadSpec<NumberingSpec>('numbering-cases.json');

describe('numbering matches spec/numbering-cases.json', () => {
  it.each(spec.format)('$pattern on $date #$seq -> $expected', ({ pattern, date, seq, expected }) => {
    expect(formatNumber(pattern, date, seq)).toBe(expected);
  });
  it.each(spec.resetKey)('$reset reset key for $date', ({ reset, date, expected }) => {
    expect(resetKey(reset, date)).toBe(expected);
  });
  it.each(spec.invalid)('rejects "$pattern" ($reset)', ({ pattern, reset }) => {
    expect(validatePattern(pattern, reset).length).toBeGreaterThan(0);
  });
  it.each(spec.valid)('accepts "$pattern" ($reset)', ({ pattern, reset }) => {
    expect(validatePattern(pattern, reset)).toEqual([]);
  });
});
