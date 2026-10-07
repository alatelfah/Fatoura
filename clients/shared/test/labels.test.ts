import { describe, expect, it } from 'vitest';
import { en, isTaxOnlyVatBox, itemLineDescription, vatBoxLabel } from '../src';

describe('itemLineDescription', () => {
  it('names the item before its description', () => {
    expect(itemLineDescription('Gas Hob 4 Burners', 'Tempered glass, cast iron supports')).toBe('Gas Hob 4 Burners - Tempered glass, cast iron supports');
  });
  it('keeps a description that already names the item', () => {
    expect(itemLineDescription('BOMPANI BUILTIN DISHWAHER', 'BOMPANI BUILTIN DISHWAHER Stainless steel 14 Place settings')).toBe(
      'BOMPANI BUILTIN DISHWAHER Stainless steel 14 Place settings',
    );
  });
  it('uses the name alone when there is no description', () => {
    expect(itemLineDescription('Installation', '  ')).toBe('Installation');
    expect(itemLineDescription('Installation', null)).toBe('Installation');
  });
});

describe('vatBoxLabel', () => {
  // A tiny stand-in for i18next's t over the English resources.
  const t = (key: string, options?: Record<string, unknown>) => {
    const value = key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], en);
    if (typeof value !== 'string') return (options?.defaultValue as string) ?? key;
    return value.replace(/\{\{(\w+)\}\}/g, (_, name: string) => String(options?.[name] ?? ''));
  };

  it('names the emirate for boxes 1a–1g', () => {
    expect(vatBoxLabel(t, '1b', 'x')).toBe('Standard rated supplies in Dubai');
    expect(vatBoxLabel(t, '1f', 'x')).toBe('Standard rated supplies in Ras Al Khaimah');
  });
  it('translates the other boxes and falls back to the server label', () => {
    expect(vatBoxLabel(t, '4', 'x')).toBe('Zero rated supplies');
    expect(vatBoxLabel(t, '99', 'Server label')).toBe('Server label');
  });
  it('marks boxes 12–14 as tax only', () => {
    expect(['11', '12', '13', '14'].map(isTaxOnlyVatBox)).toEqual([false, true, true, true]);
  });
});
