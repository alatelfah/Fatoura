import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

export function loadSpec<T>(name: string): T {
  const path = fileURLToPath(new URL(`../../../spec/${name}`, import.meta.url));
  return JSON.parse(readFileSync(path, 'utf8')) as T;
}
