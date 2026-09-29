import { NullPainter } from 'gratify';
import { describe, expect, it } from 'vitest';
import { autoLayout, tidyLayout } from '../src/autoLayout.js';
import { nodeFootprint } from '../src/nodeRender.js';
import type { CanvasModel, CanvasNode } from '../src/viewModel.js';

const measure = new NullPainter().measure;
const table = [{ name: 'table', type: 'Table' as const }];

const card = (id: string, h: number, extra: Partial<CanvasNode> = {}): CanvasNode =>
  ({ id, kind: 'test', x: 0, y: 0, w: 260, h, inputs: table, outputs: table, params: [], selected: false, ...extra });

/** Every pair of nodes whose footprints (largest style) intersect. */
function overlaps(nodes: readonly CanvasNode[], positions: Record<string, { x: number; y: number }>): string[] {
  const boxes = nodes.map(n => {
    const f = nodeFootprint(n, measure);
    return { id: n.id, x: positions[n.id]!.x + f.x, y: positions[n.id]!.y + f.y, w: f.w, h: f.h };
  });
  return boxes.flatMap((a, i) => boxes.slice(i + 1)
    .filter(b => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h)
    .map(b => `${a.id} / ${b.id}`));
}

const described = { description: 'Keeps the rows whose expression is true.' };
const nodes = ['source', 'query-a', 'query-b', 'join', 'sort'].map((id, i) => card(id, 120 + i * 20, described));
const edges = [['source', 'query-a'], ['source', 'query-b'], ['query-a', 'join'], ['query-b', 'join'], ['join', 'sort']].map(([a, b]) => ({ id: a! + b!, from: a + '.out', to: b + '.in' }));
const graph: CanvasModel = { nodes, edges, selectedEdgeId: null, openEditor: null };

describe('nodeFootprint', () => {
  it('covers the sockets and the description footer the classic style hangs below the card', () => {
    expect(nodeFootprint(card('a', 100, described), measure)).toEqual({ x: -4.5, y: 0, w: 269, h: 121 });
    expect(nodeFootprint(card('b', 100, { inputs: [], outputs: [] }), measure)).toEqual({ x: 0, y: 0, w: 260, h: 100 });
  });
});

describe('autoLayout', () => {
  it('lays out branching graphs deterministically without overlapping footprints', () => {
    for (const viewport of [{ width: 700, height: 750 }, { width: 1500, height: 500 }]) {
      const positions = autoLayout(graph, viewport);
      expect(autoLayout(graph, viewport)).toEqual(positions);
      expect(overlaps(nodes, positions)).toEqual([]);
    }
  });

  it('keeps a stacked layer clear of the description footer above it', () => {
    const tall = ['a', 'b', 'c'].map(id => card(id, 200, described));
    const positions = autoLayout({ nodes: tall, edges: [], selectedEdgeId: null, openEditor: null }, { width: 400, height: 2000 });
    expect(overlaps(tall, positions)).toEqual([]);
  });
});

describe('tidyLayout', () => {
  it('moves overlapping cards down within their column and keeps column order', () => {
    const stored = [card('a', 200, { x: 40, y: 40 }), card('b', 200, { x: 40, y: 180 }), card('c', 100, { x: 370, y: 40 }), card('d', 100, { x: 400, y: 100 })];
    const positions = tidyLayout({ nodes: stored, edges: [], selectedEdgeId: null, openEditor: null }, measure);
    expect(overlaps(stored, positions)).toEqual([]);
    expect(positions['a']).toEqual({ x: 40, y: 40 });
    expect(positions['b']!.x).toBe(40);
    expect(positions['b']!.y).toBeGreaterThan(240);
    expect(positions['c']!.x).toBeGreaterThan(300);
    expect(positions['d']!.x).toBe(positions['c']!.x);
    expect(positions['d']!.y).toBeGreaterThan(positions['c']!.y);
  });

  it('leaves a layout with room to spare unchanged', () => {
    const stored = [card('a', 100, { x: 40, y: 40 }), card('b', 100, { x: 40, y: 400 }), card('c', 100, { x: 500, y: 40 })];
    const positions = tidyLayout({ nodes: stored, edges: [], selectedEdgeId: null, openEditor: null }, measure);
    expect(positions).toEqual({ a: { x: 40, y: 40 }, b: { x: 40, y: 400 }, c: { x: 500, y: 40 } });
  });
});
