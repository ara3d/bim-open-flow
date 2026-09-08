import { expect, it } from 'vitest';
import { autoLayout } from '../src/autoLayout.js';
import type { CanvasModel } from '../src/viewModel.js';

it('lays out branching graphs deterministically without overlapping variable-height nodes', () => {
  const nodes = ['source', 'query-a', 'query-b', 'join', 'sort'].map((id, i) => ({ id, kind: 'test', x: 0, y: 0, w: 260, h: 120 + i * 20, inputs: [], outputs: [], params: [], selected: false }));
  const edges = [['source', 'query-a'], ['source', 'query-b'], ['query-a', 'join'], ['query-b', 'join'], ['join', 'sort']].map(([a, b]) => ({ id: a + b, from: a + '.out', to: b + '.in' }));
  const graph: CanvasModel = { nodes, edges, selectedEdgeId: null };
  for (const viewport of [{ width: 700, height: 750 }, { width: 1500, height: 500 }]) {
    const positions = autoLayout(graph, viewport);
    expect(autoLayout(graph, viewport)).toEqual(positions);
    for (const a of nodes) for (const b of nodes) if (a.id !== b.id) {
      const p = positions[a.id]!, q = positions[b.id]!;
      expect(p.x + a.w <= q.x || q.x + b.w <= p.x || p.y + a.h <= q.y || q.y + b.h <= p.y).toBe(true);
    }
  }
});
