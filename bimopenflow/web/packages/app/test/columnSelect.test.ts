import { expect, it, vi } from 'vitest';
import { ColumnSelects } from '../src/columnSelect.js';
import type { SuggestionList } from '@bimopenflow/contracts';
const ok = (...values: string[]): SuggestionList => ({ status: 'Ok', values: values.map(value => ({ value })) });
const tick = () => new Promise(resolve => setTimeout(resolve, 0));

it('updates columns when the upstream shape changes and preserves an invalid selection visibly', async () => {
  let data = ok('Name', 'Width');
  const commit = vi.fn();
  const controls = new ColumnSelects(async () => data, commit);
  const el = controls.get('sort', 'A', 'Width', false);
  await tick();
  const select = el.querySelector('select')!;
  expect([...select.options].map(o => o.value)).toEqual(['', 'Name', 'Width']);
  data = ok('Room', 'Area');
  controls.refresh(); await tick();
  expect([...select.options].map(o => o.textContent)).toEqual(['— None —', 'Room', 'Area', 'Width (unavailable)']);
  select.value = 'Area'; select.dispatchEvent(new Event('change'));
  expect(commit).toHaveBeenCalledWith('sort', 'A', 'Area');
  el.querySelector('button')!.click();
  expect(commit).toHaveBeenCalledWith('sort', 'descendingA', 'true');
  controls.prune(new Set());
});

it('ignores stale responses from an earlier graph and disposed controls', async () => {
  const pending: ((data: SuggestionList) => void)[] = [];
  const controls = new ColumnSelects(() => new Promise(resolve => pending.push(resolve)), () => {});
  const el = controls.get('sort', 'A', '', false);
  controls.refresh();
  pending[1]!(ok('New')); await tick();
  pending[0]!(ok('Old')); await tick();
  expect([...el.querySelector('select')!.options].map(o => o.value)).toEqual(['', 'New']);
  controls.refresh(); controls.prune(new Set());
  pending[2]!(ok('Disposed')); await tick();
  expect([...el.querySelector('select')!.options].some(o => o.value === 'Disposed')).toBe(false);
});
