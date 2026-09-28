// portY and peekTargetAt: socket geometry shared by drawing and the peek
// hit-test. Hand-built CanvasModel literals, no store or gratify runtime.

import { describe, expect, it } from "vitest";
import { NODE_HEADER, PORT_SPACING, type CanvasModel, type CanvasNode } from "../src/viewModel.js";
import { peekTargetAt, portY, SOCKET_GRAB_RADIUS, WIRE_HIT_DISTANCE } from "../src/portGeometry.js";

function node(over: Partial<CanvasNode> & { id: string }): CanvasNode {
  return {
    kind: "k.a",
    x: 0,
    y: 0,
    w: 184,
    h: 100,
    inputs: [],
    outputs: [],
    params: [],
    selected: false,
    ...over,
  };
}

function model(over: Partial<CanvasModel>): CanvasModel {
  return {
    nodes: [],
    edges: [],
    selectedEdgeId: null,
    openEditor: null,
    ...over,
  };
}

describe("portY", () => {
  it("agrees with the formula canvasParts.ts uses for indices 0 to 3", () => {
    const top = 80;
    for (let index = 0; index < 4; index++) {
      expect(portY(top, index)).toBe(top + NODE_HEADER + (index + 0.5) * PORT_SPACING);
    }
  });
});

describe("peekTargetAt", () => {
  // Node a at (80, 80), width 184, one output "out": socket at (264, 138).
  const a = node({
    id: "a",
    x: 80,
    y: 80,
    w: 184,
    outputs: [{ name: "out", type: "Table" }],
  });

  it("finds the output socket within SOCKET_GRAB_RADIUS", () => {
    const m = model({ nodes: [a] });
    expect(peekTargetAt(m, 266, 139)).toBe("a.out");
  });

  it("gives null when no socket or wire is near", () => {
    const m = model({ nodes: [a] });
    expect(peekTargetAt(m, 150, 139)).toBeNull();
  });

  it("falls back to a wire's source endpoint", () => {
    const b = node({
      id: "b",
      x: 400,
      y: 80,
      w: 184,
      inputs: [{ name: "in", type: "Table" }],
    });
    const m = model({
      nodes: [a, b],
      edges: [{ id: "a.out->b.in", from: "a.out", to: "b.in" }],
    });
    // The wire runs roughly horizontally from (264, 138) to (400, 138); a
    // point near its midpoint, off any socket, should hit the wire.
    const midX = (264 + 400) / 2;
    expect(peekTargetAt(m, midX, 138)).toBe("a.out");
  });

  it("never targets an input socket", () => {
    const b = node({
      id: "b",
      x: 400,
      y: 80,
      w: 184,
      inputs: [{ name: "in", type: "Table" }],
    });
    const m = model({ nodes: [a, b] });
    // (400, 138) is b's input socket position; with no wire nearby it must
    // stay null rather than resolve to the input.
    expect(peekTargetAt(m, 400, 138)).toBeNull();
  });

  it("has SOCKET_GRAB_RADIUS 12 and WIRE_HIT_DISTANCE 8, matching canvasParts.ts", () => {
    expect(SOCKET_GRAB_RADIUS).toBe(12);
    expect(WIRE_HIT_DISTANCE).toBe(8);
  });
});
