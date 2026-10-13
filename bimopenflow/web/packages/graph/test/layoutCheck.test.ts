import { describe, expect, it } from 'vitest';
import type { NodeDescriptor } from '@bimopenflow/contracts';
import { initialState, reduce, type Action } from '@bimopenflow/state';
import { autoLayout } from '../src/autoLayout.js';
import { connectionIssues, isBrokenLayout, layoutIssues } from '../src/layoutCheck.js';
import { buildCanvasModel, type CanvasModel, type CanvasNode } from '../src/viewModel.js';

const table = [{ name: 'table', type: 'Table' as const }];
const card = (id: string, x: number, y: number): CanvasNode =>
  ({ id, kind: 'test', x, y, w: 200, h: 100, inputs: table, outputs: table, params: [], selected: false });
const model = (nodes: CanvasNode[], wires: [string, string][] = []): CanvasModel => ({
  nodes, edges: wires.map(([a, b]) => ({ id: `${a}-${b}`, from: `${a}.table`, to: `${b}.table` })), selectedEdgeId: null, openEditor: null,
});
const kinds = (m: CanvasModel) => layoutIssues(m).map(i => `${i.kind}: ${i.text}`);

describe('layoutIssues', () => {
  it('finds overlapping cards', () => {
    const m = model([card('a', 0, 0), card('b', 150, 50)]);
    expect(kinds(m)).toEqual(['overlap: a / b']);
    expect(isBrokenLayout(m)).toBe(true);
  });

  it('finds a wire drawn behind a third card', () => {
    expect(kinds(model([card('a', 0, 0), card('mid', 300, 0), card('b', 600, 0)], [['a', 'b']])))
      .toEqual(['wireBehindCard: a.table -> b.table behind mid']);
  });

  it('does not blame the layout for a wire from a rail card, which the editor places', () => {
    const m = model([{ ...card('$model', 0, 0), inRail: true }, card('mid', 300, 0), card('b', 600, 0)], [['$model', 'b']]);
    expect(kinds(m)).toEqual([]);
  });

  it('finds a wire running right to left', () => {
    expect(kinds(model([card('a', 400, 0), card('b', 0, 0)], [['a', 'b']]))).toContain('backwardWire: a.table -> b.table');
  });

  it('passes a clean chain', () => {
    const m = model([card('a', 0, 0), card('b', 300, 0), card('c', 600, 0)], [['a', 'b'], ['b', 'c']]);
    expect(kinds(m)).toEqual([]);
    expect(isBrokenLayout(m)).toBe(false);
  });
});

describe('connectionIssues', () => {
  const kind = (name: string, inputs: NodeDescriptor['inputs']): NodeDescriptor =>
    ({ kind: name, version: 1, capability: 'Pure', params: [], description: '', inputs, outputs: [{ name: 'table', type: 'Table', optional: false }] });
  const catalog = new Map<string, NodeDescriptor>([
    ['k.source', kind('k.source', [])],
    ['k.step', kind('k.step', [{ name: 'table', type: 'Table', optional: false }, { name: 'extra', type: 'Table', optional: true }])],
  ]);
  const doc = (actions: Action[]) => actions.reduce(reduce, initialState).document;

  it('names an unwired required input and a node with no wire, and skips optional inputs', () => {
    const document = doc([
      { type: 'addNode', id: 'src', kind: 'k.source', version: 1 },
      { type: 'addNode', id: 'step', kind: 'k.step', version: 1 },
      { type: 'addNode', id: 'lone', kind: 'k.source', version: 1 },
      { type: 'addNode', id: 'idle', kind: 'k.step', version: 1 },
      { type: 'connect', from: 'src.table', to: 'step.table' },
    ]);
    expect(connectionIssues(document, catalog).map(i => `${i.kind}: ${i.text}`)).toEqual([
      'isolated: lone (k.source)', 'unwiredInput: idle.table (k.step)', 'isolated: idle (k.step)',
    ]);
  });
});

describe('autoLayout passes its own check', () => {
  // The shape of bim-door-rooms (2026-10-12): one path feeding two readers, a
  // filter on one branch, both meeting at a two-input step, then a chain.
  const catalog = new Map<string, NodeDescriptor>([
    ['k.read', { kind: 'k.read', version: 1, capability: 'Pure', params: [], description: 'Reads.', inputs: [{ name: 'path', type: 'Table', optional: false }], outputs: [{ name: 'table', type: 'Table', optional: false }] }],
    ['k.pair', { kind: 'k.pair', version: 1, capability: 'Pure', params: [], description: 'Pairs.', inputs: [{ name: 'a', type: 'Table', optional: false }, { name: 'b', type: 'Table', optional: false }], outputs: [{ name: 'table', type: 'Table', optional: false }] }],
    ['k.source', { kind: 'k.source', version: 1, capability: 'Pure', params: [], description: 'Holds.', inputs: [], outputs: [{ name: 'table', type: 'Table', optional: false }] }],
    ['view.note', { kind: 'view.note', version: 1, capability: 'Pure', params: [{ name: 'text', kind: 'Text', default: '' }], description: '', inputs: [], outputs: [] }],
  ]);
  const add = (id: string, kind: string): Action => ({ type: 'addNode', id, kind, version: 1 });
  const wire = (from: string, to: string): Action => ({ type: 'connect', from, to });
  const document = [
    add('about', 'view.note'), add('model', 'k.source'), add('bounds', 'k.read'), add('doors', 'k.read'),
    add('overlap', 'k.pair'), add('touching', 'k.read'), add('named', 'k.read'), add('answer', 'k.read'), add('rooms', 'k.read'),
    wire('model.table', 'bounds.path'), wire('model.table', 'rooms.path'), wire('bounds.table', 'doors.path'),
    wire('doors.table', 'overlap.a'), wire('rooms.table', 'overlap.b'), wire('overlap.table', 'touching.path'),
    wire('touching.table', 'named.path'), wire('named.table', 'answer.path'),
  ].reduce(reduce, initialState).document;

  it('lays it out with no overlap, no wire behind a card, and no backward wire', () => {
    const m = buildCanvasModel({ ...initialState, document }, catalog, null);
    const at = autoLayout(m);
    const placed = { ...m, nodes: m.nodes.map(n => ({ ...n, ...at[n.id] })) };
    expect(kinds(placed)).toEqual([]);
    expect(at['about']!.y).toBeLessThan(at['model']!.y);
    expect(at['rooms']!.x).toBe(at['doors']!.x);
  });
});
