import { NullPainter, type Measure } from 'gratify';
import { nodeFootprint, type CardBox } from './nodeRender.js';
import type { CanvasModel, CanvasNode } from './viewModel.js';

type Positions = Record<string, { x: number; y: number }>;

/** Space between two cards in one layer or column, and between layers. */
const LAYOUT_GAP_ACROSS = 48;
const LAYOUT_GAP_ALONG = 64;
const MARGIN = 24;

/** NullPainter's measure: a footprint depends on which texts a card has, not
 *  on their width, so the fixed measure gives the same boxes as the canvas. */
const defaultMeasure: Measure = new NullPainter().measure;

/** Each card's footprint in its largest style (nodeRender.nodeFootprint): a
 *  layout that keeps these apart has no overlap in any style (TKT-110). */
function footprints(model: CanvasModel, measure: Measure): Map<string, CardBox> {
  return new Map(model.nodes.map(node => [node.id, nodeFootprint(node, measure)]));
}

/** Layered layout, following the earlier PlatoFlow depth/centering approach.
 * Choose horizontal or vertical flow to use the available panel without overlapping cards. */
export function autoLayout(model: CanvasModel, viewport: { width: number; height: number }, measure: Measure = defaultMeasure): Positions {
  const boxes = footprints(model, measure);
  const nodes = new Set(model.nodes.map(node => node.id));
  const parents = new Map(model.nodes.map(node => [node.id, [] as string[]]));
  for (const edge of model.edges) {
    const from = edge.from.slice(0, edge.from.lastIndexOf('.'));
    const to = edge.to.slice(0, edge.to.lastIndexOf('.'));
    if (nodes.has(from) && nodes.has(to)) parents.get(to)!.push(from);
  }
  const depths = new Map<string, number>();
  const remaining = new Set(nodes);
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
    // Footprint coordinates: `result` holds where each footprint's corner goes.
    const result: Positions = {};
    const breadth = (id: string) => vertical ? boxes.get(id)!.w : boxes.get(id)!.h;
    const length = (id: string) => vertical ? boxes.get(id)!.h : boxes.get(id)!.w;
    const widths = layers.map(layer => layer.reduce((sum, id) => sum + breadth(id), 0) + Math.max(0, layer.length - 1) * LAYOUT_GAP_ACROSS);
    const widest = Math.max(0, ...widths);
    let along = MARGIN;
    for (const [index, layer] of layers.entries()) {
      const center = (id: string) => {
        const upstream = parents.get(id)!.filter(parent => result[parent]);
        return upstream.length ? upstream.reduce((sum, parent) => sum + (vertical ? result[parent]!.x : result[parent]!.y) + breadth(parent) / 2, 0) / upstream.length : 0;
      };
      const ordered = [...layer].sort((a, b) => center(a) - center(b) || a.localeCompare(b));
      let across = MARGIN + (widest - widths[index]!) / 2;
      for (const id of ordered) {
        result[id] = vertical ? { x: across, y: along } : { x: along, y: across };
        across += breadth(id) + LAYOUT_GAP_ACROSS;
      }
      along += Math.max(0, ...layer.map(length)) + LAYOUT_GAP_ALONG;
    }
    const width = Math.max(1, ...model.nodes.map(node => result[node.id]!.x + boxes.get(node.id)!.w));
    const height = Math.max(1, ...model.nodes.map(node => result[node.id]!.y + boxes.get(node.id)!.h));
    return { result: cardCorners(result, boxes), scale: Math.min((viewport.width - 48) / width, (viewport.height - 72) / height) };
  };
  const horizontal = arrange(false), vertical = arrange(true);
  return vertical.scale > horizontal.scale ? vertical.result : horizontal.result;
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
export function tidyLayout(model: CanvasModel, measure: Measure = defaultMeasure, columnSnap = 100): Positions {
  const boxes = footprints(model, measure);
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
