/**
 * Document number patterns mirrored from backend `Fatoura.Domain.Numbering.NumberPatternFormatter`.
 * Tokens: {YYYY} {YY} {MM} {MON} {DD} {SEQ} {SEQ:n}. Verified against spec/numbering-cases.json.
 */
export type SequenceReset = 'Never' | 'Yearly' | 'Monthly';
export type DocumentType = 'Invoice' | 'Quotation' | 'CreditNote' | 'Purchase';

export const PATTERN_MAX_LENGTH = 40;
const TOKEN = /\{([A-Z]+)(?::(\d+))?\}/g;
const KNOWN = new Set(['YYYY', 'YY', 'MM', 'MON', 'DD', 'SEQ']);
const MONTHS = ['JAN', 'FEB', 'MAR', 'APR', 'MAY', 'JUN', 'JUL', 'AUG', 'SEP', 'OCT', 'NOV', 'DEC'];

const pad = (n: number, width: number) => String(n).padStart(width, '0');

/** `date` is an ISO date string (yyyy-mm-dd). */
export function formatNumber(pattern: string, date: string, seq: number): string {
  const errors = validatePattern(pattern, 'Never');
  if (errors.length > 0) throw new Error(errors[0]);
  const [y, m, d] = date.split('-').map(Number) as [number, number, number];
  return pattern.replace(TOKEN, (_all, token: string, width?: string) => {
    switch (token) {
      case 'YYYY':
        return pad(y, 4);
      case 'YY':
        return pad(y % 100, 2);
      case 'MM':
        return pad(m, 2);
      case 'MON':
        return MONTHS[m - 1]!;
      case 'DD':
        return pad(d, 2);
      default:
        return pad(seq, width ? Number(width) : 1);
    }
  });
}

export function resetKey(reset: SequenceReset, date: string): string {
  if (reset === 'Never') return '-';
  if (reset === 'Yearly') return date.slice(0, 4);
  return date.slice(0, 7);
}

export function validatePattern(pattern: string | null | undefined, reset: SequenceReset): string[] {
  const errors: string[] = [];
  if (!pattern || !pattern.trim()) return ['Pattern is required.'];
  if (pattern.length > PATTERN_MAX_LENGTH) errors.push(`Pattern must be at most ${PATTERN_MAX_LENGTH} characters.`);

  const matches = [...pattern.matchAll(TOKEN)];
  const tokens = matches.map((m) => m[1]!);
  for (const t of new Set(tokens)) if (!KNOWN.has(t)) errors.push(`Unknown token {${t}}.`);
  for (const m of matches) {
    if (m[1] !== 'SEQ' && m[2] !== undefined) errors.push(`Only {SEQ} accepts a width ({${m[1]}:${m[2]}}).`);
    else if (m[2] !== undefined && (Number(m[2]) < 1 || Number(m[2]) > 10))
      errors.push('{SEQ:n} width must be between 1 and 10.');
  }
  if (tokens.filter((t) => t === 'SEQ').length !== 1)
    errors.push('Pattern must contain exactly one {SEQ} or {SEQ:n} token.');
  if (/[{}]/.test(pattern.replace(TOKEN, ''))) errors.push('Braces are only allowed around tokens.');

  const hasYear = tokens.includes('YYYY') || tokens.includes('YY');
  const hasMonth = tokens.includes('MM') || tokens.includes('MON');
  if (reset === 'Yearly' && !hasYear)
    errors.push('A yearly reset needs {YYYY} or {YY} in the pattern so numbers stay unique.');
  if (reset === 'Monthly' && !(hasYear && hasMonth))
    errors.push('A monthly reset needs a year token and {MM} or {MON} in the pattern so numbers stay unique.');
  return errors;
}
