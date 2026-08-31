// Gratify parts for the graph canvas: surface (grid + pan/zoom + key
// fallback), node (ports as anchors, drag-to-move, drag-to-wire), wire, and
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
  rgb,
  Stack,
  v,
  Vec,
  vdist,
  wireDist,
} from "gratify";
import type { NodeStatus, PortType } from "@bimopenflow/contracts";
import type { CanvasEdge, CanvasModel, CanvasNode } from "./viewModel.js";
import { NODE_HEADER, PORT_SPACING } from "./viewModel.js";
import { anchorId, canConnect, parseAnchorId, type CanvasIntent } from "./canvasIntents.js";

const SOCKET_RADIUS = 4.5;
const SOCKET_GRAB_RADIUS = 12;

const STATUS_COLORS: Record<NodeStatus, Color> = {
  Ok: rgb(59, 165, 93),
  Unready: rgb(150, 148, 140),
  EffectPending: rgb(217, 154, 43),
  Unavailable: rgb(120, 140, 170),
  Error: rgb(192, 57, 43),
};

// ── Surface ──────────────────────────────────────────────────────────────────

interface SurfaceProps {
  selectedEdgeId: string | null;
}

const Surface = part<SurfaceProps, { gridDot: Color }>("bof-surface", {
  style: (t) => ({ gridDot: calpha(t.muted, 0.3) }),
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

// ── Node ─────────────────────────────────────────────────────────────────────

type NodeProps = CanvasNode & { pos: Vec; states?: Record<string, boolean> };

interface NodeStyle {
  fill: Color;
  edge: Color;
  text: Color;
  dim: Color;
  lift: number;
  socket: Color;
}

const portY = (top: number, index: number): number =>
  top + NODE_HEADER + (index + 0.5) * PORT_SPACING;

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

const GraphNodePart = part<NodeProps, NodeStyle>("bof-node", {
  size: (p) => v(p.w, p.h),
  anchors: portAnchors,

  style: (t, channels) => ({
    fill: t.mix(t.surface, t.surfaceHi, 0.4 * channels.hover + 0.6 * channels.drag),
    edge: t.mix(t.muted, t.accent, (channels.sel || 0) + 0.5 * channels.hover),
    text: t.mix(t.text, t.textBright, channels.hover),
    dim: t.muted,
    lift: 3 * channels.drag,
    socket: t.accent,
  }),

  render(node, painter, style) {
    const r = node.rect.raise(style.lift);
    const p = node.props;
    painter.box(r, 8, style.fill, style.edge, 1.2 + (node.ch.sel || 0) * 1.2);
    painter.label(p.id, v(r.x + 10, r.y + NODE_HEADER / 2), style.text, {
      align: "left",
      weight: 600,
      size: 12,
    });
    painter.label(p.kind, v(r.x + 10, r.y + NODE_HEADER + 2), style.dim, {
      align: "left",
      size: 9,
    });
    if (p.status)
      painter.dot(v(r.right - 10, r.y + NODE_HEADER / 2), 4, STATUS_COLORS[p.status]);
    p.inputs.forEach((port, i) => {
      const y = portY(r.y, i);
      painter.dot(v(r.x, y), SOCKET_RADIUS, style.socket);
      painter.label(port.name, v(r.x + 8, y), style.dim, { align: "left", size: 9 });
    });
    p.outputs.forEach((port, i) => {
      const y = portY(r.y, i);
      painter.dot(v(r.right, y), SOCKET_RADIUS, style.socket);
      painter.label(port.name, v(r.right - 8, y), style.dim, { align: "right", size: 9 });
    });
  },

  on: [
    // Wire drag: starts only when the press lands near a socket.
    Gesture<NodeProps, { fromId: string; cursor: Vec; snap?: Anchor }>({
      begin(node, pointer, query) {
        for (const a of portAnchors(node)) {
          const live = query.anchor(a.id);
          if (live && vdist(live.pos, pointer) < SOCKET_GRAB_RADIUS)
            return { fromId: live.id, cursor: pointer };
        }
        return null;
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
        if (!state.snap) return;
        return { kind: "connect", a: state.fromId, b: state.snap.id } satisfies CanvasIntent;
      },
    }),

    // Node move: transient "move" intents while dragging, one "moveEnd" commit.
    Gesture<NodeProps, { grabOffset: Vec }>({
      begin: (node, pointer) => ({
        grabOffset: v(pointer.x - node.props.pos.x, pointer.y - node.props.pos.y),
      }),
      during: (state, node, pointer) =>
        ({
          kind: "move",
          id: node.props.id,
          x: pointer.x - state.grabOffset.x,
          y: pointer.y - state.grabOffset.y,
        }) satisfies CanvasIntent,
      up: (_state, node) =>
        [
          { kind: "moveEnd", id: node.props.id },
          { kind: "selectNode", id: node.props.id },
        ] satisfies CanvasIntent[],
    }),
  ],
});

// ── Wires ────────────────────────────────────────────────────────────────────

interface WireProps {
  id: string;
  from: string; // anchor id
  to: string;
  states?: Record<string, boolean>;
}

const Wire = part<WireProps, { color: Color; selected: number }>("bof-wire", {
  style: (t, channels) => {
    const selected = channels.sel || 0;
    return {
      color: selected > 0.02 ? t.mix(t.accent, rgb(255, 200, 80), selected) : t.accent,
      selected,
    };
  },

  hit(node, pointer) {
    const a = node.anchor?.(node.props.from);
    const b = node.anchor?.(node.props.to);
    return !!a && !!b && wireDist(a, b, pointer) < 8;
  },

  render(node, painter, style) {
    const a = node.anchor?.(node.props.from);
    const b = node.anchor?.(node.props.to);
    if (!a || !b) return;
    painter.wire(a, b, calpha(rgb(0, 0, 0), 0.25), 4);
    painter.wire(a, b, calpha(style.color, 0.9), 2 + 1.4 * style.selected + 0.8 * node.ch.hover);
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
  style: (t) => ({ color: t.accent }),
  render(node, painter, style) {
    const color = node.props.snapped ? rgb(90, 220, 130) : calpha(style.color, 0.8);
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

export function canvasView(model: CanvasModel): Element {
  return Surface("root", { selectedEdgeId: model.selectedEdgeId }, [
    Free("graph", {}, [
      ...model.edges.map((edge) => {
        const { from, to } = wireAnchorIds(edge);
        return Wire(edge.id, {
          id: edge.id,
          from,
          to,
          states: { sel: model.selectedEdgeId === edge.id },
        });
      }),
      ...model.nodes.map((n) =>
        GraphNodePart(n.id, { ...n, pos: v(n.x, n.y), states: { sel: n.selected } })),
    ]),
    onScreenLayer(
      Stack("hud", { pad: 10 }, [
        Label("hint", {
          text: "drag node · drag socket = wire · click wire + Del = cut · drag/wheel = pan/zoom",
          dim: true,
          size: 11,
        }),
      ]),
    ),
  ]);
}
