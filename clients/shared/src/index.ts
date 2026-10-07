export * from './calc';
export * from './trn';
export * from './numbering';
export * from './api/client';
export { default as en } from './i18n/en';
export type { Translations } from './i18n/en';
export { default as ar } from './i18n/ar';

const EMIRATE_BY_BOX: Record<string, string> = {
  '1a': 'AbuDhabi', '1b': 'Dubai', '1c': 'Sharjah', '1d': 'Ajman', '1e': 'UmmAlQuwain', '1f': 'RasAlKhaimah', '1g': 'Fujairah',
};

/** Translated description of a VAT 201 box ("1b", "4", …); falls back to the server's English label. */
export function vatBoxLabel(t: (key: string, options?: Record<string, unknown>) => string, box: string, fallback: string): string {
  const emirate = EMIRATE_BY_BOX[box];
  if (emirate) return t('reports.vatBox.1', { emirate: t(`settings.${emirate}`) });
  return t(`reports.vatBox.${box}`, { defaultValue: fallback });
}

/** VAT 201 boxes 12–14 carry only a tax amount; their net cell is left blank. */
export function isTaxOnlyVatBox(box: string): boolean {
  return box === '12' || box === '13' || box === '14';
}

/**
 * Line description for a catalogue item: its name, followed by its description unless that already names it
 * (so printed documents always say what was sold).
 */
export function itemLineDescription(name: string, description?: string | null): string {
  const d = description?.trim();
  if (!d) return name;
  return d.toLowerCase().includes(name.trim().toLowerCase()) ? d : `${name} - ${d}`;
}
