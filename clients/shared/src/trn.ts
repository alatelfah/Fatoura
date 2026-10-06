/**
 * UAE TRN rule mirrored from backend `Fatoura.Domain.Validation.TrnValidator`:
 * exactly 15 digits starting with "10" (spaces and dashes ignored). Verified against spec/trn-cases.json.
 */
export const TRN_LENGTH = 15;
export const TRN_PREFIX = '10';

export function normalizeTrn(value: string | null | undefined): string {
  return (value ?? '').replace(/[ -]/g, '');
}

export function isValidTrn(value: string | null | undefined): boolean {
  const trn = normalizeTrn(value);
  return trn.length === TRN_LENGTH && /^\d+$/.test(trn) && trn.startsWith(TRN_PREFIX);
}
