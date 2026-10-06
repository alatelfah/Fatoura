import { describe, expect, it, vi } from 'vitest';
import type { FormInstance } from 'antd';
import { applyProblem } from './problems';

describe('applyProblem', () => {
  it('maps API field errors (including line paths) onto form fields', () => {
    const setFields = vi.fn();
    const form = { setFields } as unknown as FormInstance;
    const message = applyProblem(
      { title: 'One or more validation errors occurred.', errors: { 'lines[0].quantity': ['Quantity must be greater than zero.'], trn: ['TRN must be 15 digits starting with 10.'] } },
      form,
    );
    expect(message).toBe('One or more validation errors occurred.');
    expect(setFields).toHaveBeenCalledWith([
      { name: ['lines', 0, 'quantity'], errors: ['Quantity must be greater than zero.'] },
      { name: ['trn'], errors: ['TRN must be 15 digits starting with 10.'] },
    ]);
  });

  it('returns the problem title when there are no field errors', () => {
    expect(applyProblem({ title: 'This invoice is already void.', status: 409 })).toBe('This invoice is already void.');
  });
});
