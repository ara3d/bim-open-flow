import { v, wireAt } from 'gratify';
import { nodeFootprint, type CardBox } from './nodeRender.js';
import { NOTE_KIND } from './nodeSize.js';
import { socketOffset } from './portGeometry.js';
import type { CanvasModel, CanvasNode } from './viewModel.js';

type Positions = Record<string, { x: number; y: number }>;

/** Space between the canvas origin and the first card. */
const MARGIN = 40;
/** Space between two layers, left to right: room for the wires to bend. */
const GAP_ALONG = 80;
/** Space between two cards in a layer, and between a card or a lane and a lane. */
const GAP_CARD = 40;
const GAP_LANE = 12;
/** Height a wire that passes through a layer reserves there. */
const LANE = 8;
/** Rounds of ordering sweeps and of alignment passes. */
const ORDER_SWEEPS = 8;
const ALIGN_ROUNDS = 16;

/** The model without the sources rail (TKT-138): its nodes keep the rail's place whatever layout runs. */
const mainFlow = (model: CanvasModel): CanvasModel => ({ ...model, nodes: model.nodes.filter(node => !node.inRail) });

/** Each card's footprint (nodeRender.nodeFootprint): a layout that keeps
 *  these apart has no overlap (TKT-110). */
function footprints(model: CanvasModel): Map<string, CardBox> {
  return new Map(model.nodes.map(node => [node.id, nodeFootprint(node)]));
}

/** One thing a layer holds: a card, or a lane a wire passes through. */
interface Item { readonly id: string; readonly h: number; readonly lane: boolean }

/** One wire between neighbouring layers: from `a` (at `ay` below its top) to
 *  `b` (at `by` below its top). A wire across several layers becomes a chain
 *  of these through lanes. */
interface Segment { readonly a: string; readonly b: string; readonly ay: number; readonly by: number; readonly weight: number }

const nodeOf = (endpoint: string) => endpoint.slice(0, endpoint.lastIndexOf('.'));
const portOf = (endpoint: string) => endpoint.slice(endpoint.lastIndexOf('.') + 1);

/** Layer of each node: one more than its deepest parent; then, from the
 *  outputs back, a node moves right to just before its nearest child, so a
 *  source sits beside the step that uses it rather than far to the left
 *  with a long wire across the graph. Nodes on a cycle take layer 0. */
function layers(ids: readonly string[], parents: ReadonlyMap<string, string[]>, children: ReadonlyMap<string, string[]>): Map<string, number> {
  const layer = new Map<string, number>();
  const order: string[] = [];
  const remaining = new Set(ids);
  while (remaining.size) {
    const ready = [...remaining].filter(id => parents.get(id)!.every(p => layer.has(p)));
    if (!ready.length) { for (const id of remaining) { layer.set(id, 0); order.push(id); } break; }
    for (const id of ready) {
      layer.set(id, Math.max(-1, ...parents.get(id)!.map(p => layer.get(p)!)) + 1);
      order.push(id);
      remaining.delete(id);
    }
  }
  for (const id of order.reverse()) {
    const next = children.get(id)!.map(c => layer.get(c)!).filter(l => l > layer.get(id)!);
    if (next.length) layer.set(id, Math.min(...next) - 1);
  }
  return layer;
}

/** Number of crossings among `segments` (all leaving one layer), each end
 *  ranked by its item's place and then its socket. */
function crossings(segments: readonly Segment[], rank: ReadonlyMap<string, number>): number {
  const ends = segments.map(s => [rank.get(s.a)! * 1e4 + s.ay, rank.get(s.b)! * 1e4 + s.by] as const);
  let count = 0;
  for (const [i, [a1, b1]] of ends.entries())
    for (const [a2, b2] of ends.slice(i + 1)) if ((a1 - a2) * (b1 - b2) < 0) count++;
  return count;
}

/** The heights minimising the weighted squared distance to `want`, keeping
 *  each item at least `space[i]` below the one before it: isotonic
 *  regression, by pooling adjacent violators, over height minus the space
 *  above. */
function packToward(want: readonly number[], weight: readonly number[], space: readonly number[]): number[] {
  const offset: number[] = [];
  space.forEach((s, i) => offset.push(i === 0 ? 0 : offset[i - 1]! + s));
  const blocks: { value: number; weight: number; count: number }[] = [];
  want.forEach((w, i) => {
    blocks.push({ value: w - offset[i]!, weight: weight[i]!, count: 1 });
    while (blocks.length > 1 && blocks.at(-2)!.value > blocks.at(-1)!.value) {
      const b = blocks.pop()!, a = blocks.pop()!;
      const sum = a.weight + b.weight;
      blocks.push({ value: (a.value * a.weight + b.value * b.weight) / sum, weight: sum, count: a.count + b.count });
    }
  });
  return blocks.flatMap(b => Array<number>(b.count).fill(b.value)).map((z, i) => z + offset[i]!);
}

/** Layered layout, left to right (Sugiyama's method, kept small): layers by
 *  longest path, a lane for each wire that skips a layer so no wire is drawn
 *  behind a card, barycentre sweeps that keep the order with fewest
 *  crossings, and cards slid up or down so each wire runs as straight as it
 *  can between its two sockets. Notes go in a row above the flow. Cards are
 *  spaced by their painted footprints, so nothing overlaps.
 *
 *  Flow is always left to right, the way the sockets face (inputs on the
 *  left edge, outputs on the right): stacking layers top to bottom bent every
 *  wire back on itself. `_viewport` is unused since that choice went. */
export function autoLayout(canvas: CanvasModel, _viewport?: { width: number; height: number }): Positions {
  const model = mainFlow(canvas);
  const boxes = footprints(model);
  const byId = new Map(model.nodes.map(n => [n.id, n]));
  const notes = model.nodes.filter(n => n.kind === NOTE_KIND);
  const flow = model.nodes.filter(n => n.kind !== NOTE_KIND);
  const docOrder = new Map(model.nodes.map((n, i) => [n.id, i]));
  const inFlow = (id: string) => byId.has(id) && byId.get(id)!.kind !== NOTE_KIND;
  const wires = model.edges
    .map(e => ({ from: nodeOf(e.from), to: nodeOf(e.to), out: portOf(e.from), in: portOf(e.to) }))
    .filter(w => w.from !== w.to && inFlow(w.from) && inFlow(w.to));
  const parents = new Map(flow.map(n => [n.id, [] as string[]]));
  const children = new Map(flow.map(n => [n.id, [] as string[]]));
  for (const w of wires) { parents.get(w.to)!.push(w.from); children.get(w.from)!.push(w.to); }
  const layerOf = layers(flow.map(n => n.id), parents, children);

  // Items per layer, and the segments leaving each layer.
  const depth = Math.max(-1, ...layerOf.values()) + 1;
  const items: Item[][] = Array.from({ length: depth }, () => []);
  for (const n of flow) items[layerOf.get(n.id)!]!.push({ id: n.id, h: boxes.get(n.id)!.h, lane: false });
  const leaving: Segment[][] = Array.from({ length: depth }, () => []);
  const socketY = (id: string, port: string, dir: 'in' | 'out') =>
    socketOffset(byId.get(id)!, port, dir)?.y ?? boxes.get(id)!.h / 2;
  wires.forEach((w, index) => {
    const from = layerOf.get(w.from)!, to = layerOf.get(w.to)!;
    if (to <= from) return; // a wire on a cycle is drawn where it falls
    let a = w.from, ay = socketY(w.from, w.out, 'out'), weight = 1;
    for (let l = from + 1; l < to; l++) {
      const lane = `~${index}~${l}`;
      items[l]!.push({ id: lane, h: LANE, lane: true });
      leaving[l - 1]!.push({ a, b: lane, ay, by: LANE / 2, weight: 30 });
      a = lane; ay = LANE / 2; weight = 30;
    }
    leaving[to - 1]!.push({ a, b: w.to, ay, by: socketY(w.to, w.in, 'in'), weight });
  });
  const entering = (l: number): readonly Segment[] => l === 0 ? [] : leaving[l - 1]!;

  // Order within each layer: barycentre sweeps down and up, keeping the
  // order with fewest crossings.
  const rank = new Map<string, number>();
  const setRanks = () => items.forEach(layer => layer.forEach((item, i) => rank.set(item.id, i)));
  const firstPlace = (id: string) => docOrder.get(id) ?? Infinity;
  items[0]?.sort((a, b) => firstPlace(a.id) - firstPlace(b.id));
  setRanks();
  /** Sorts layer `l` by the mean place of the far ends of `toward`. */
  const sortBy = (l: number, toward: readonly Segment[], far: 'a' | 'b') => {
    const near = far === 'a' ? 'b' : 'a';
    const centre = new Map(items[l]!.map(item => {
      const ends = toward.filter(s => s[near] === item.id).map(s => rank.get(s[far])! + (far === 'a' ? s.ay : s.by) / 1e4);
      return [item.id, ends.length ? ends.reduce((x, y) => x + y, 0) / ends.length : rank.get(item.id)!];
    }));
    items[l]!.sort((a, b) => centre.get(a.id)! - centre.get(b.id)! || firstPlace(a.id) - firstPlace(b.id));
    items[l]!.forEach((item, i) => rank.set(item.id, i));
  };
  const totalCrossings = () => leaving.reduce((sum, segments) => sum + crossings(segments, rank), 0);
  let best = items.map(layer => [...layer]), bestCount = totalCrossings();
  for (let sweep = 0; sweep < ORDER_SWEEPS && bestCount > 0; sweep++) {
    for (let l = 1; l < depth; l++) sortBy(l, entering(l), 'a');
    for (let l = depth - 2; l >= 0; l--) sortBy(l, leaving[l]!, 'b');
    const count = totalCrossings();
    if (count < bestCount) { best = items.map(layer => [...layer]); bestCount = count; }
  }
  best.forEach((layer, l) => { items[l] = layer; });

  // Heights: stack each layer, then slide items so wires run straight.
  const y = new Map<string, number>();
  const space = (layer: readonly Item[]) => layer.map((item, i) => {
    const above = layer[i - 1];
    if (!above) return 0;
    return above.h + (above.lane && item.lane ? GAP_LANE : above.lane || item.lane ? GAP_LANE + LANE : GAP_CARD);
  });
  const place = (layer: readonly Item[], want: readonly number[], weight: readonly number[]) =>
    packToward(want, weight, space(layer)).forEach((v, i) => y.set(layer[i]!.id, v));
  for (const layer of items) place(layer, layer.map(() => 0), layer.map(() => 1));
  /** Moves layer `l` toward where the far ends of `toward` want it. */
  const align = (l: number, toward: readonly Segment[], far: 'a' | 'b') => {
    const layer = items[l]!;
    const wants = layer.map(item => {
      const linked = toward.filter(s => (far === 'a' ? s.b : s.a) === item.id);
      if (!linked.length) return { at: y.get(item.id)!, weight: 0.01 };
      const weight = linked.reduce((sum, s) => sum + s.weight, 0);
      const at = linked.reduce((sum, s) => sum + s.weight * (far === 'a' ? y.get(s.a)! + s.ay - s.by : y.get(s.b)! + s.by - s.ay), 0) / weight;
      return { at, weight };
    });
    place(layer, wants.map(w => w.at), wants.map(w => w.weight));
  };
  for (let round = 0; round < ALIGN_ROUNDS; round++) {
    if (round % 2 === 0) for (let l = 1; l < depth; l++) align(l, entering(l), 'a');
    else for (let l = depth - 2; l >= 0; l--) align(l, leaving[l]!, 'b');
  }

  // Left to right: each layer as wide as its widest card.
  const layerX: number[] = [];
  let x = MARGIN;
  for (const layer of items) {
    layerX.push(x);
    x += Math.max(0, ...layer.filter(item => !item.lane).map(item => boxes.get(item.id)!.w)) + GAP_ALONG;
  }
  const spans: WireSpan[] = wires.flatMap(w => {
    const from = layerOf.get(w.from)!, to = layerOf.get(w.to)!;
    if (to <= from) return [];
    return [{
      from: w.from, to: w.to, fromLayer: from, toLayer: to,
      ax: layerX[from]! - boxes.get(w.from)!.x + byId.get(w.from)!.w, ay: socketY(w.from, w.out, 'out'),
      bx: layerX[to]! - boxes.get(w.to)!.x, by: socketY(w.to, w.in, 'in'),
    }];
  });
  clearWires(items.map(layer => layer.filter(item => !item.lane)), layerX, boxes, spans, y);

  // Place: notes in a row at the top, the flow below them.
  const result: Positions = {};
  let noteX = MARGIN;
  for (const note of notes) {
    result[note.id] = { x: noteX, y: MARGIN };
    noteX += boxes.get(note.id)!.w + GAP_CARD;
  }
  const top = MARGIN + (notes.length ? Math.max(...notes.map(n => boxes.get(n.id)!.h)) + GAP_CARD : 0);
  const cardIds = flow.map(n => n.id);
  const highest = Math.min(...cardIds.map(id => y.get(id)!));
  for (const [l, layer] of items.entries())
    for (const item of layer) if (!item.lane) result[item.id] = { x: layerX[l]!, y: Math.round(top + y.get(item.id)! - highest) };
  return cardCorners(result, boxes);
}

/** A wire as drawn once layers have their x: from the output socket at
 *  (ax, top of `from` + ay) to the input socket at (bx, top of `to` + by). */
interface WireSpan {
  readonly from: string; readonly to: string;
  readonly fromLayer: number; readonly toLayer: number;
  readonly ax: number; readonly ay: number; readonly bx: number; readonly by: number;
}

/** Space kept between a wire and a card it passes. */
const WIRE_CLEARANCE = 12;
/** Rounds of clearing: moving a card moves its own wires too. */
const CLEAR_ROUNDS = 6;
/** Points sampled along each wire. */
const WIRE_SAMPLES = 48;

/** Moves cards up or down, keeping their order in the layer, until no wire
 *  is drawn behind a card that is not one of its ends. A wire is drawn as
 *  one curve between its sockets (gratify's wireAt), not through the lanes
 *  the alignment kept for it, so the lanes alone do not keep it clear where
 *  its two ends sit at different heights or one socket feeds several wires.
 *  Updates `y` (footprint tops) in place. */
function clearWires(
  layers: readonly Item[][], layerX: readonly number[], boxes: ReadonlyMap<string, CardBox>,
  spans: readonly WireSpan[], y: Map<string, number>,
): void {
  for (let round = 0; round < CLEAR_ROUNDS; round++) {
    let moved = false;
    for (const [l, layer] of layers.entries()) {
      // Where each wire runs over this layer: its y range within the x range of each card.
      const curves = spans.filter(s => s.fromLayer <= l && l < s.toLayer).map(s => ({
        span: s,
        points: Array.from({ length: WIRE_SAMPLES + 1 }, (_, i) =>
          wireAt(v(s.ax, y.get(s.from)! + s.ay), v(s.bx, y.get(s.to)! + s.by), i / WIRE_SAMPLES)),
      }));
      let floor = -Infinity;
      for (const item of [...layer].sort((a, b) => y.get(a.id)! - y.get(b.id)!)) {
        const box = boxes.get(item.id)!;
        const left = layerX[l]!, right = left + box.w;
        const blocks = curves.filter(c => c.span.from !== item.id && c.span.to !== item.id).flatMap(c => {
          const over = c.points.filter(p => p.x > left && p.x < right).map(p => p.y);
          return over.length ? [{ top: Math.min(...over) - WIRE_CLEARANCE, bottom: Math.max(...over) + WIRE_CLEARANCE }] : [];
        });
        const start = y.get(item.id)!;
        let at = Math.max(start, floor);
        let wentUp = false;
        for (let guard = 0; guard < 64; guard++) {
          const hit = blocks.find(b => b.top < at + box.h && at < b.bottom);
          if (!hit) break;
          const up = hit.top - box.h;
          if (!wentUp && up >= floor && start - up < hit.bottom - start) { at = up; wentUp = true; }
          else at = hit.bottom;
        }
        if (Math.abs(at - start) > 0.5) moved = true;
        y.set(item.id, at);
        floor = at + box.h + GAP_CARD;
      }
    }
    if (!moved) return;
  }
}

/** Card positions from footprint positions. */
function cardCorners(footprintAt: Positions, boxes: ReadonlyMap<string, CardBox>): Positions {
  return Object.fromEntries(Object.entries(footprintAt).map(([id, p]) => {
    const box = boxes.get(id)!;
    return [id, { x: p.x - box.x, y: p.y - box.y }];
  }));
}

/** Least space tidyLayout keeps between two columns and between two cards
 *  in a column: room for the wires, and no more, so a graph that already
 *  fits is left alone. Positions it has to move land on a TIDY_GRID step. */
const TIDY_GAP_COLUMN = 40;
const TIDY_GAP_ROW = 24;
const TIDY_GRID = 10;

/** `stored` when it already clears `least`; otherwise the first TIDY_GRID
 *  step at or after `least`. */
const pushTo = (stored: number, least: number) => stored >= least ? stored : Math.ceil(least / TIDY_GRID) * TIDY_GRID;

/** Tidies a laid-out graph without reordering it (TKT-110): cards whose left
 *  edges lie within `columnSnap` of each other share a column, left-aligned.
 *  Columns keep their left-to-right order and cards their top-to-bottom order;
 *  each card keeps its stored position unless the footprints before it push
 *  it right or down, so a graph with no overlap keeps its shape. */
export function tidyLayout(canvas: CanvasModel, columnSnap = 100): Positions {
  const model = mainFlow(canvas);
  const boxes = footprints(model);
  const box = (node: CanvasNode) => boxes.get(node.id)!;
  const byX = [...model.nodes].sort((a, b) => a.x - b.x || a.y - b.y || a.id.localeCompare(b.id));
  const columns: CanvasNode[][] = [];
  for (const node of byX) {
    const column = columns.at(-1);
    if (column && node.x - column[0]!.x < columnSnap) column.push(node);
    else columns.push([node]);
  }
  const result: Positions = {};
  let right = -Infinity;
  for (const column of columns) {
    // Card x of the column: its leftmost stored x, or the least x that clears
    // the previous column's footprints.
    const left = Math.max(...column.map(n => -box(n).x));
    const x = pushTo(Math.min(...column.map(n => n.x)), right + TIDY_GAP_COLUMN + left);
    let bottom = -Infinity;
    for (const node of [...column].sort((a, b) => a.y - b.y || a.x - b.x || a.id.localeCompare(b.id))) {
      const y = pushTo(node.y, bottom + TIDY_GAP_ROW - box(node).y);
      result[node.id] = { x, y };
      bottom = y + box(node).y + box(node).h;
    }
    right = x + Math.max(...column.map(n => box(n).x + box(n).w));
  }
  return result;
}
