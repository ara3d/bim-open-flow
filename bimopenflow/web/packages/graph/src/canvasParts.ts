// Gratify parts for the graph canvas: surface (grid + pan/zoom + key
// fallback), node (ports as anchors, drag-to-move, drag-to-wire, corner
// drag-to-resize), wire, and
// the rubber-wire preview. Patterns adapted from gratify's node-editor
// example; all state changes travel as CanvasIntents (see canvasIntents.ts).

import {
  Anchor,
  calpha,
  Color,
  Element,
  Free,
  Gesture,
  GNode,
  Keys,
  Label,
  Pan,
  part,
  Press,
  rect,
  Stack,
  themeVersion,
  v,
  Vec,
  vdist,
  wireAt,
  wireDist,
  withExt,
} from "gratify";
import type { PortType } from "@bimopenflow/contracts";
import type { CanvasEdge, CanvasModel, CanvasNode } from "./viewModel.js";
import type { WireRows } from "./portResults.js";
import { nodeSize, NOTE_KIND, NOTE_LINE_H, NOTE_PAD, NOTE_TEXT_SIZE } from "./nodeSize.js";
import { resizeHandleHit } from "./canvasResize.js";
import { placeSlots, SLOT_X_PAD } from "./canvasSlots.js";
import { slotElement } from "./slotRegistry.js";
import { canvasColors, canvasThemes, currentCanvasTheme } from "./canvasTheme.js";
import { currentNodeStyle } from "./nodeStyle.js";
import { nodePack } from "./graphPreview.js";
import { portsBottom, renderNodeCard, type NodeCardColors } from "./nodeRender.js";
import { animateSelection, selectionBorder } from "./selectionBorder";
import { upstreamEdges } from "./upstreamEdges.js";
const selectedBorder = selectionBorder(() => canvasColors().wireSelected, animateSelection);
import { anchorId, canConnect, parseAnchorId, type CanvasIntent } from "./canvasIntents.js";
import { grabRadius, portY, SOCKET_GRAB_RADIUS, WIRE_HIT_DISTANCE } from "./portGeometry.js";
import { peekAdorn, rowCountText } from "./peekCard.js";
import { wrapParagraphs } from "./canvasLongSlot.js";
import { createLongValueEditor, type LongValueEditor } from "./longValueEditor.js";
import { styleIsland } from "./slotShared.js";
import type { CanvasInstance } from "./instance.js";

// ── Surface ──────────────────────────────────────────────────────────────────

interface SurfaceProps {
  selectedEdgeId: string | null;
}

const Surface = part<SurfaceProps, { gridDot: Color }>("bof-surface", {
  style: () => ({ gridDot: canvasColors().gridDot }),
  measure: (_p, avail) => avail,
  hit: () => true,

  render(node, painter, style) {
    const viewport = node.view!;
    const SPACING = 28;
    const left = Math.floor(-viewport.pan.x / viewport.zoom / SPACING) * SPACING;
    const right = (viewport.w - viewport.pan.x) / viewport.zoom;
    const top = Math.floor(-viewport.pan.y / viewport.zoom / SPACING) * SPACING;
    const bottom = (viewport.h - viewport.pan.y) / viewport.zoom;
    for (let x = left; x <= right; x += SPACING)
      for (let y = top; y <= bottom; y += SPACING)
        painter.dot(v(x, y), 1, style.gridDot);
  },

  on: [
    Pan(),
    Press(() => ({ kind: "clearSelection" }) satisfies CanvasIntent),
    Keys({
      Delete: () => ({ kind: "deleteSelected" }) satisfies CanvasIntent,
      Backspace: () => ({ kind: "deleteSelected" }) satisfies CanvasIntent,
    }),
  ],
});

// ── Note (view.note) ────────────────────────────────────────────────────────
// A view.note card skips ports, the status dot, and the kind/id header the
// way every other node draws them: it is the note's whole text, pale yellow,
// wrapped over up to NOTE_MAX_LINES lines. Editing reuses longValueEditor.ts
// the same way canvasLongSlot.ts's row editor does, but the island covers the
// whole card (there is no separate label row to click) and its lifetime is
// keyed by nodeId alone, pruned every render from the live note ids — a
// view.note has no params, so it never appears in canvasEditor's per-param
// liveKeys set that prunes ordinary rows.

function styleNoteTextarea(editor: LongValueEditor): void {
  const ta = editor.textarea;
  styleIsland(ta, canvasThemes[currentCanvasTheme()].palette);
  ta.style.flex = "1 1 auto";
  ta.style.resize = "none";
}

function noteEditorFor(instance: CanvasInstance, node: CanvasNode): LongValueEditor {
  let editor = instance.noteEditors.get(node.id);
  if (!editor) {
    editor = createLongValueEditor(instance.document);
    instance.noteEditors.set(node.id, editor);
  }
  if (instance.noteEditorThemeV.get(node.id) !== themeVersion) {
    instance.noteEditorThemeV.set(node.id, themeVersion);
    styleNoteTextarea(editor);
  }
  if (!editor.isOpen()) {
    editor.open({
      label: "Note",
      value: node.noteText ?? "",
      onCommit: (value) =>
        instance.dispatch({ kind: "setParam", nodeId: node.id, name: "text", value } satisfies CanvasIntent),
      onClose: () => instance.dispatch({ kind: "closeEditor" } satisfies CanvasIntent),
    });
  }
  return editor;
}

/** Disposes the instance's note editors for ids no longer on the canvas;
 *  called from canvasView every render (see the comment above) as well as
 *  from the editor's pruning. */
function pruneNoteEditors(instance: CanvasInstance, liveIds: ReadonlySet<string>): void {
  for (const [id, editor] of instance.noteEditors) {
    if (!liveIds.has(id)) {
      editor.dispose();
      instance.noteEditors.delete(id);
      instance.noteEditorThemeV.delete(id);
    }
  }
}

// ── Node ─────────────────────────────────────────────────────────────────────

type NodeProps = CanvasNode & { pos: Vec; instance: CanvasInstance; states?: Record<string, boolean>; noteOpen?: boolean };

interface AnchorMeta {
  dir: "in" | "out";
  nodeId: string;
  type: PortType;
}

const metaOf = (a: Anchor): AnchorMeta => a.meta as AnchorMeta;

interface PortAnchor {
  id: string;
  pos: Vec;
  meta: AnchorMeta;
}

const portAnchors = (node: GNode<NodeProps>): PortAnchor[] => {
  const r = node.rect;
  const p = node.props;
  return [
    ...p.inputs.map((port, i) => ({
      id: anchorId("in", p.id, port.name),
      pos: v(r.x, portY(r.y, i)),
      meta: { dir: "in" as const, nodeId: p.id, type: port.type },
    })),
    ...p.outputs.map((port, i) => ({
      id: anchorId("out", p.id, port.name),
      pos: v(r.right, portY(r.y, i)),
      meta: { dir: "out" as const, nodeId: p.id, type: port.type },
    })),
  ];
};

const anchorEnd = (a: Anchor) => metaOf(a);

const GraphNodePart = part<NodeProps, NodeCardColors>("bof-node", {
  channels: { enter: { target: () => 1, rate: 1000 } },
  size: (p) => v(p.w, p.h),
  anchors: portAnchors,

  // Children are the inline param slots, stacked below the port rows; each
  // slot's height comes from its control kind (canvasSlots.placeSlots).
  arrange(props, r, kids) {
    const placements = placeSlots(props.params, portsBottom(props)).slots;
    return kids.map((_kid, i) => {
      const p = placements[i]!;
      return rect(r.x + SLOT_X_PAD, r.y + p.y, r.w - 2 * SLOT_X_PAD, p.h);
    });
  },

  style: (t, channels, props) => {
    if (props.kind === NOTE_KIND) {
      const c = canvasColors();
      return {
        fill: c.noteFill,
        edge: t.mix(c.noteEdge, t.accent, channels.sel || 0),
        text: c.noteText,
        dim: c.noteText,
        socket: c.noteText,
      };
    }
    return {
      fill: t.mix(t.surface, t.surfaceHi, 0.4 * channels.hover + 0.6 * channels.drag),
      edge: t.mix(t.muted, t.accent, (channels.sel || 0) + 0.5 * channels.hover),
      text: t.mix(t.text, t.textBright, 0.4 + 0.6 * channels.hover),
      dim: t.textDim,
      socket: t.accent,
    };
  },

  render(node, painter, style) {
    const r = node.rect;
    const p = node.props;
    if (p.kind === NOTE_KIND) {
      painter.box(r, 6, style.fill, style.edge, p.contributing ? 2 : 1.2);
      // As many lines as the card's height holds: NOTE_MAX_LINES at the
      // default height, more once the note is resized taller.
      const maxLines = Math.max(1, Math.floor((r.h - 2 * NOTE_PAD) / NOTE_LINE_H));
      const lines = wrapParagraphs(painter.measure, p.noteText ?? "", r.w - 2 * NOTE_PAD, NOTE_TEXT_SIZE, maxLines);
      lines.forEach((line, i) => {
        painter.label(line, v(r.x + NOTE_PAD, r.y + NOTE_PAD + NOTE_LINE_H * (i + 0.7)), style.text, {
          align: "left",
          size: NOTE_TEXT_SIZE,
        });
      });
    } else {
      const c = canvasColors();
      renderNodeCard(node, painter, style, { status: c.status, contributing: c.wireSelected, packAccent: c.packAccents[nodePack(p.kind)] }, currentNodeStyle());
    }
    // The resize grip: only where a drag would work, and only on the cards
    // the pointer or the selection is on, so a full canvas is not dotted
    // with grips.
    if (!p.instance.readOnly && (p.selected || (node.ch.hover ?? 0) > 0.5)) {
      const grip = calpha(style.dim, 0.8);
      painter.line(v(r.right - 3, r.bottom - 10), v(r.right - 10, r.bottom - 3), grip, 1.2);
      painter.line(v(r.right - 3, r.bottom - 6), v(r.right - 6, r.bottom - 3), grip, 1.2);
    }
  },

  on: [
    // Resize drag (TKT-124): starts only when the press lands on the
    // bottom-right corner, so it runs ahead of the wire and move gestures.
    // The corner's reach is in screen pixels when zoomed out (TKT-125).
    // Transient "resize" intents while dragging, one "resizeEnd" commit. The
    // part API has no cursor, so the grip drawn in render is the only cue.
    Gesture<NodeProps, { origin: Vec; start: { w: number; h: number } }>({
      begin(node, pointer) {
        const p = node.props;
        const card = { x: p.pos.x, y: p.pos.y, w: p.w, h: p.h };
        const sockets = portAnchors(node).map((a) => a.pos);
        if (p.instance.readOnly || !resizeHandleHit(card, pointer, node.view?.zoom ?? 1, sockets)) return null;
        return { origin: pointer, start: { w: p.w, h: p.h } };
      },
      during: (state, node, pointer) => ({
        kind: "resize",
        id: node.props.id,
        ...nodeSize(node.props, {
          w: state.start.w + pointer.x - state.origin.x,
          h: state.start.h + pointer.y - state.origin.y,
        }),
      }) satisfies CanvasIntent,
      up: (state, node) =>
        (node.props.w !== state.start.w || node.props.h !== state.start.h
          ? { kind: "resizeEnd", id: node.props.id }
          : { kind: "selectNode", id: node.props.id }) satisfies CanvasIntent,
    }),

    // Wire drag: starts only when the press lands near a socket, the
    // nearest one when a zoomed-out reach covers several (TKT-125).
    Gesture<NodeProps, { fromId: string; cursor: Vec; snap?: Anchor }>({
      begin(node, pointer, query) {
        if (node.props.instance.readOnly) return null;
        const reach = grabRadius(SOCKET_GRAB_RADIUS, node.view?.zoom ?? 1);
        const live = portAnchors(node)
          .map((a) => query.anchor(a.id))
          .filter((a): a is Anchor => a !== undefined && vdist(a.pos, pointer) < reach)
          .sort((a, b) => vdist(a.pos, pointer) - vdist(b.pos, pointer))[0];
        return live ? { fromId: live.id, cursor: pointer } : null;
      },
      move: (state, _node, pointer, query) => ({
        ...state,
        cursor: pointer,
        snap: query.nearestAnchor(pointer, 26, (candidate) =>
          candidate.meta !== undefined &&
          canConnect(
            { ...parseAnchorId(state.fromId), type: metaOf(query.anchor(state.fromId)!).type },
            anchorEnd(candidate),
          )),
      }),
      view(state, query) {
        const from = query.anchor(state.fromId);
        if (!from) return [];
        return [
          RubberWire("bof-rubber-wire", {
            a: from.pos,
            b: state.snap?.pos ?? state.cursor,
            snapped: state.snap !== undefined,
          }),
        ];
      },
      up(state) {
        if (!state.snap)
          return { kind: "wireDropped", from: state.fromId, x: state.cursor.x, y: state.cursor.y } satisfies CanvasIntent;
        return { kind: "connect", a: state.fromId, b: state.snap.id } satisfies CanvasIntent;
      },
    }),

    // Node move: transient "move" intents while dragging, one "moveEnd" commit.
    Gesture<NodeProps, { grabOffset: Vec; start: Vec }>({
      begin: (node, pointer) => ({
        grabOffset: v(pointer.x - node.props.pos.x, pointer.y - node.props.pos.y),
        start: node.props.pos,
      }),
      during: (state, node, pointer) => node.props.instance.readOnly ? undefined :
        ({
          kind: "move",
          id: node.props.id,
          x: pointer.x - state.grabOffset.x,
          y: pointer.y - state.grabOffset.y,
        }) satisfies CanvasIntent,
      up: (state, node) => node.props.instance.readOnly
        ? [{ kind: "selectNode", id: node.props.id } satisfies CanvasIntent]
        : [vdist(state.start,node.props.pos) > 3
        ? { kind: "moveEnd", id: node.props.id }
        // A click (not a drag) on a note opens its editor directly — there is
        // no separate label row to press, the way a long-text param has.
        : node.props.kind === NOTE_KIND
          ? { kind: "openEditor", nodeId: node.props.id, name: "text" }
          : { kind: "selectNode", id: node.props.id }] satisfies CanvasIntent[],
    }),
  ],

  island(node) {
    if (node.props.kind !== NOTE_KIND) return null;
    if (!node.props.noteOpen) {
      node.props.instance.noteEditors.get(node.props.id)?.close();
      return null;
    }
    const editor = noteEditorFor(node.props.instance, node.props);
    const r = node.rect;
    return { el: editor.el, rect: rect(r.x, r.y, r.w, r.h) };
  },
});

// ── Wires ────────────────────────────────────────────────────────────────────

interface WireProps {
  id: string;
  from: string; // anchor id
  to: string;
  states?: Record<string, boolean>;
  contributing?: boolean;
  /** On the upstream path that feeds the selected node, or a node the host
   *  is currently evaluating (TKT-24). Painted as a travelling highlight, or
   *  a static one under prefers-reduced-motion. */
  flowing?: boolean;
  /** Row count of the table or relation leaving this wire's source port;
   *  absent draws no label. `current: false` dims the label while a new
   *  count is in flight or the document is dirty (TKT-11). */
  rows?: WireRows;
}

/** How far along the wire (0..1) the travelling highlight sits at `time`,
 *  looping every second so the eye reads it as flowing a -> b. */
const flowT = (time: number): number => time - Math.floor(time);

const Wire = part<WireProps, { color: Color; selected: number; label: Color; labelDim: Color }>("bof-wire", {
  style: (t, channels) => {
    const selected = channels.sel || 0;
    const c = canvasColors();
    return {
      color: selected > 0.02 ? t.mix(c.wire, c.wireSelected, selected) : c.wire,
      selected,
      label: t.textDim,
      labelDim: calpha(t.textDim, 0.5),
    };
  },

  hit(node, pointer) {
    const a = node.anchor?.(node.props.from);
    const b = node.anchor?.(node.props.to);
    return !!a && !!b && wireDist(a, b, pointer) < WIRE_HIT_DISTANCE;
  },

  render(node, painter, style) {
    const a = node.anchor?.(node.props.from);
    const b = node.anchor?.(node.props.to);
    if (!a || !b) return;
    painter.wire(a, b, canvasColors().wireShadow, 4);
    painter.wire(a, b, node.props.contributing ? canvasColors().wireSelected : calpha(style.color, 0.9), node.props.contributing ? 3 : 2 + 1.4 * style.selected + 0.8 * node.ch.hover);
    if (node.props.flowing) {
      const flow = canvasColors().wireFlow;
      if (animateSelection()) {
        const t = flowT((node.time ?? 0) / 1.4);
        painter.dot(wireAt(a, b, t), 3.4, flow);
        painter.dot(wireAt(a, b, flowT(t + 0.5)), 3.4, calpha(flow, 0.55));
      } else {
        painter.wire(a, b, calpha(flow, 0.85), 3);
      }
    }
    if (node.props.rows) {
      const mid = v((a.x + b.x) / 2, (a.y + b.y) / 2);
      const color = node.props.rows.current ? style.label : style.labelDim;
      painter.label(rowCountText(node.props.rows.rows), mid, color, { size: 10 });
    }
  },

  on: [
    Press((node: GNode<WireProps>) =>
      ({ kind: "selectEdge", id: node.props.id }) satisfies CanvasIntent),
  ],
});

interface RubberWireProps {
  a: Vec;
  b: Vec;
  snapped: boolean;
}

const RubberWire = part<RubberWireProps, { color: Color }>("bof-rubber-wire", {
  style: () => ({ color: canvasColors().wire }),
  render(node, painter, style) {
    const color = node.props.snapped ? canvasColors().rubberSnap : calpha(style.color, 0.8);
    painter.wire(node.props.a, node.props.b, color, node.props.snapped ? 2.6 : 2);
    painter.dot(node.props.b, 4, color);
  },
});

// ── View ─────────────────────────────────────────────────────────────────────

const onScreenLayer = (element: Element): Element => ({ ...element, layer: "screen" });

const wireAnchorIds = (edge: CanvasEdge) => {
  // Edge endpoints are "nodeId.port"; anchors add the direction prefix.
  return { from: `out:${edge.from}`, to: `in:${edge.to}` };
};

export function canvasView(model: CanvasModel, instance: CanvasInstance): Element {
  // The animated path (TKT-24): every wire feeding the selected node, so the
  // owner can see at a glance what flows into it. Wired off `selected`, the
  // same per-node flag selectionBorder pulses, not the 3D-view "contributing"
  // preview above (a different, pre-existing highlight).
  const selectedId = model.nodes.find((n) => n.selected)?.id ?? null;
  const flowing = upstreamEdges(model, selectedId);
  pruneNoteEditors(instance, new Set(model.nodes.filter((n) => n.kind === NOTE_KIND).map((n) => n.id)));
  return Surface("root", { selectedEdgeId: model.selectedEdgeId }, [
    Free("graph", {}, [
      ...model.edges.map((edge) => {
        const { from, to } = wireAnchorIds(edge);
        return Wire(edge.id, {
          id: edge.id,
          from,
          to,
          contributing: edge.contributing,
          flowing: flowing.has(edge.id),
          rows: edge.rows,
          states: { sel: model.selectedEdgeId === edge.id },
        });
      }),
      ...model.nodes.map((n) =>
        withExt(GraphNodePart(
          n.id,
          {
            ...n,
            pos: v(n.x, n.y),
            instance,
            states: { sel: n.selected },
            noteOpen: model.openEditor?.nodeId === n.id && model.openEditor.name === "text",
          },
          n.params.map((param) => slotElement({
            nodeId: n.id,
            param,
            instance,
            w: n.w - 2 * SLOT_X_PAD,
            open: model.openEditor?.nodeId === n.id && model.openEditor.name === param.name,
          })),
        ), selectedBorder, peekAdorn(model.peek))),
    ]),
    onScreenLayer(
      Stack("hud", { pad: 10 }, [
        Label("hint", {
          text: instance.readOnly
            ? "click = select · drag/wheel = pan/zoom"
            : "drag node · drag socket = wire · click wire + Del = cut · drag/wheel = pan/zoom",
          dim: true,
          size: 11,
        }),
      ]),
    ),
  ]);
}
