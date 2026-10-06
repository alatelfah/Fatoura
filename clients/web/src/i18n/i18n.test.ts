import { describe, expect, it } from 'vitest';
import { ar, en } from '@fatoura/shared';
import { applyLanguage } from './index';

function keys(obj: object, prefix = ''): string[] {
  return Object.entries(obj).flatMap(([k, v]) => (typeof v === 'object' ? keys(v as object, `${prefix}${k}.`) : [`${prefix}${k}`]));
}

describe('translations', () => {
  it('Arabic covers every English key with a non-empty string', () => {
    expect(keys(ar).sort()).toEqual(keys(en).sort());
    for (const k of keys(ar)) {
      const value = k.split('.').reduce<unknown>((o, p) => (o as Record<string, unknown>)[p], ar);
      expect(String(value).length, k).toBeGreaterThan(0);
    }
  });

  it('switching language updates document direction', () => {
    applyLanguage('ar');
    expect(document.documentElement.dir).toBe('rtl');
    expect(document.documentElement.lang).toBe('ar');
    applyLanguage('en');
    expect(document.documentElement.dir).toBe('ltr');
  });
});
