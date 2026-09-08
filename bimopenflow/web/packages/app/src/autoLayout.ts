import type { CanvasModel } from './viewModel.js';

/** Layered layout, following the earlier PlatoFlow depth/centering approach.
 * Choose horizontal or vertical flow to use the available panel without overlapping cards. */
export function autoLayout(model: CanvasModel, viewport: { width: number; height: number }): Record<string, { x: number; y: number }> {
  const nodes = new Map(model.nodes.map(node => [node.id, node]));
  const parents = new Map(model.nodes.map(node => [node.id, [] as string[]]));
  for (const edge of model.edges) {
    const from = edge.from.slice(0, edge.from.lastIndexOf('.'));
    const to = edge.to.slice(0, edge.to.lastIndexOf('.'));
    if (nodes.has(from) && nodes.has(to)) parents.get(to)!.push(from);
  }
  const depths = new Map<string, number>();
  const remaining = new Set(nodes.keys());
  while (remaining.size) {
    const ready = [...remaining].filter(id => parents.get(id)!.every(parent => depths.has(parent)));
    if (!ready.length) { for (const id of remaining) depths.set(id, 0); break; }
    for (const id of ready) {
      depths.set(id, Math.max(-1, ...parents.get(id)!.map(parent => depths.get(parent)!)) + 1);
      remaining.delete(id);
    }
  }
  const layers: string[][] = [];
  for (const [id, depth] of depths) (layers[depth] ??= []).push(id);
  const arrange = (vertical: boolean) => {
    const result: Record<string, { x: number; y: number }> = {};
    const breadth = (id: string) => vertical ? nodes.get(id)!.w : nodes.get(id)!.h;
    const length = (id: string) => vertical ? nodes.get(id)!.h : nodes.get(id)!.w;
    const widths = layers.map(layer => layer.reduce((sum, id) => sum + breadth(id), 0) + Math.max(0, layer.length - 1) * 48);
    const widest = Math.max(0, ...widths);
    let along = 24;
    for (const [index, layer] of layers.entries()) {
      const center = (id: string) => {
        const upstream = parents.get(id)!.filter(parent => result[parent]);
        return upstream.length ? upstream.reduce((sum, parent) => sum + (vertical ? result[parent]!.x : result[parent]!.y) + breadth(parent) / 2, 0) / upstream.length : 0;
      };
      const ordered = [...layer].sort((a, b) => center(a) - center(b) || a.localeCompare(b));
      let across = 24 + (widest - widths[index]!) / 2;
      for (const id of ordered) {
        result[id] = vertical ? { x: across, y: along } : { x: along, y: across };
        across += breadth(id) + 48;
      }
      along += Math.max(0, ...layer.map(length)) + 64;
    }
    const width = Math.max(1, ...model.nodes.map(node => result[node.id]!.x + node.w));
    const height = Math.max(1, ...model.nodes.map(node => result[node.id]!.y + node.h));
    return { result, scale: Math.min((viewport.width - 48) / width, (viewport.height - 72) / height) };
  };
  const horizontal = arrange(false), vertical = arrange(true);
  return vertical.scale > horizontal.scale ? vertical.result : horizontal.result;
}
