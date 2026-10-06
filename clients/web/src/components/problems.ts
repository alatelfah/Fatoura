import type { FormInstance } from 'antd';
import { readProblem } from '@fatoura/shared';

/**
 * Applies an API problem to a form: field errors go next to their inputs ("lines[0].quantity" → ['lines', 0, 'quantity']),
 * anything else is returned as a message for a toast.
 */
export function applyProblem(error: unknown, form?: FormInstance): string {
  const { message, fields } = readProblem(error);
  const entries = Object.entries(fields);
  if (form && entries.length > 0) {
    form.setFields(
      entries.map(([key, msg]) => ({
        name: key.split(/[.[\]]/).filter(Boolean).map((p) => (/^\d+$/.test(p) ? Number(p) : p)),
        errors: [msg],
      })),
    );
  }
  return entries.length > 0 && form ? message : entries.map(([, m]) => m).join(' ') || message;
}
