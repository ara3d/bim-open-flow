import { describe, expect, it, vi } from "vitest";
import { createViewPane3D, inferFormat } from "../src/viewPane3D";
import type { ColorableGroup, GroupEntityMap } from "../src/instanceTable";
import type { View3DDeps, ViewerRig } from "../src/viewerDeps";
import { conformance } from "./conformance";
import { collect, fakeCtx, makeSlice, settle } from "./helpers";

const recordingGroup = (colors: number[], transforms?: number[]) => {
  const applied: Float32Array[] = [];
  const appliedTransforms: Array<{ index: number; matrix: Float32Array }> = [];
  const group: ColorableGroup = {
    instanceCount: colors.length / 4,
    colors: Float32Array.from(colors),
    setColors: (_start, c) => applied.push(c.slice()),
    ...(transforms
      ? {
          transforms: Float32Array.from(transforms),
          setTransform: (index: number, matrix: Float32Array) =>
            appliedTransforms.push({ index, matrix: matrix.slice() }),
        }
      : {}),
  };
  return { group, applied, appliedTransforms };
};

interface FakeRig extends ViewerRig {
  calls: Array<{ url: string; format: string }>;
  boxes: Array<{ transforms: Float32Array; colors: Float32Array }>;
  boxClears: number;
  renders: number;
  disposed: boolean;
  pick: (entityId: number | null) => void;
}

const fakeDeps = (
  maps: readonly GroupEntityMap[] = [],
  fail = false,
): { deps: View3DDeps; rig: () => FakeRig } => {
  let rig: FakeRig | null = null;
  return {
    deps: {
      createRig: (_canvas, onPick) => {
        rig = {
          calls: [],
          boxes: [],
          boxClears: 0,
          renders: 0,
          disposed: false,
          pick: onPick,
          load(url, format) {
            rig!.calls.push({ url, format });
            return fail ? Promise.reject(new Error("nope")) : Promise.resolve(maps);
          },
          setBoxes: (transforms, colors) =>
            void rig!.boxes.push({ transforms: transforms.slice(), colors: colors.slice() }),
          clearBoxes: () => void rig!.boxClears++,
          requestRender: () => void rig!.renders++,
          dispose: () => void (rig!.disposed = true),
        };
        return rig;
      },
    },
    rig: () => rig!,
  };
};

conformance({
  name: "ViewPane3D (fake rig)",
  make: () => createViewPane3D({ deps: fakeDeps().deps }),
  input: { kind: "model", url: "model.bos" },
});

const colorSlice = makeSlice(
  [
    ["entityId", "Integer"],
    ["r", "Number"],
    ["g", "Number"],
    ["b", "Number"],
    ["a", "Number"],
  ],
  [[7, 1, 0, 0, 1]],
);

describe("ViewPane3D", () => {
  it("infers the model format from the URL", () => {
    expect(inferFormat("a/b/model.bos")).toBe("bos");
    expect(inferFormat("Model.BOS?v=2")).toBe("bos");
    expect(inferFormat("model.glb")).toBe("glb");
    expect(inferFormat("model.gltf#frag")).toBe("glb");
  });

  it("loads via ctx.resolveAsset with the inferred format and emits modelLoaded", async () => {
    const { deps, rig } = fakeDeps();
    const pane = createViewPane3D({ deps });
    const { events, handler } = collect();
    pane.onEvent(handler);
    pane.mount(document.createElement("div"), fakeCtx());
    pane.update({ kind: "model", url: "model.bos" });
    await settle();
    expect(rig().calls).toEqual([{ url: "asset:model.bos", format: "bos" }]);
    expect(events).toEqual([
      { kind: "action", action: "modelLoaded", payload: { url: "model.bos" } },
    ]);
    pane.destroy();
    expect(rig().disposed).toBe(true);
  });

  it("emits loadError when loading rejects", async () => {
    const { deps } = fakeDeps([], true);
    const pane = createViewPane3D({ deps });
    const { events, handler } = collect();
    pane.onEvent(handler);
    pane.mount(document.createElement("div"), fakeCtx());
    pane.update({ kind: "model", url: "broken.glb" });
    await settle();
    expect(events).toEqual([
      {
        kind: "action",
        action: "loadError",
        payload: { url: "broken.glb", message: "Error: nope" },
      },
    ]);
    pane.destroy();
  });

  it("applies instance colors and isolation to loaded groups", async () => {
    const { group, applied } = recordingGroup([0.5, 0.5, 0.5, 1, 0.5, 0.5, 0.5, 1]);
    const { deps, rig } = fakeDeps([{ group, entities: [7, 8] }]);
    const pane = createViewPane3D({ deps });
    pane.mount(document.createElement("div"), fakeCtx());
    pane.update({ kind: "model", url: "m.bos" });
    await settle();
    pane.update({ kind: "instances", data: colorSlice });
    expect(applied.length).toBe(1);
    expect([...applied[0].slice(0, 4)]).toEqual([1, 0, 0, 1]); // entity 7 colored
    expect(applied[0][7]).toBe(0); // entity 8 not in the table: hidden
    expect(rig().renders).toBeGreaterThan(0);
    pane.destroy();
  });

  it("holds an instances input that arrives before the model finishes", async () => {
    const { group, applied } = recordingGroup([0.5, 0.5, 0.5, 1]);
    const { deps } = fakeDeps([{ group, entities: [7] }]);
    const pane = createViewPane3D({ deps });
    pane.mount(document.createElement("div"), fakeCtx());
    pane.update({ kind: "instances", data: colorSlice }); // before any model
    expect(applied.length).toBe(0);
    pane.update({ kind: "model", url: "m.bos" });
    await settle();
    expect(applied.length).toBe(1);
    expect([...applied[0]]).toEqual([1, 0, 0, 1]);
    pane.destroy();
  });

  it("applies offsets by rewriting instance transforms from the loaded base", async () => {
    const identity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
    const { group, appliedTransforms } = recordingGroup(
      [0.5, 0.5, 0.5, 1, 0.5, 0.5, 0.5, 1],
      [...identity, ...identity],
    );
    const { deps } = fakeDeps([{ group, entities: [7, 8] }]);
    const pane = createViewPane3D({ deps });
    pane.mount(document.createElement("div"), fakeCtx());
    pane.update({ kind: "model", url: "m.bos" });
    await settle();
    const offsetSlice = makeSlice(
      [
        ["entityId", "Integer"],
        ["offsetX", "Number"],
        ["offsetY", "Number"],
        ["offsetZ", "Number"],
      ],
      [
        [7, 10, 0, 0],
        [8, 0, 20, 0],
      ],
    );
    pane.update({ kind: "instances", data: offsetSlice });
    expect(appliedTransforms.length).toBe(2);
    expect(appliedTransforms[0].index).toBe(0);
    expect([...appliedTransforms[0].matrix.slice(12, 15)]).toEqual([10, 0, 0]);
    expect([...appliedTransforms[1].matrix.slice(12, 15)]).toEqual([0, 20, 0]);
    // a later plan without offsets restores the base transforms
    appliedTransforms.length = 0;
    pane.update({
      kind: "instances",
      data: makeSlice([["entityId", "Integer"]], [[7], [8]]),
    });
    expect(appliedTransforms.length).toBe(2);
    expect([...appliedTransforms[0].matrix]).toEqual(identity);
    pane.destroy();
  });

  it("does not rewrite transforms for plans without offsets", async () => {
    const { group, appliedTransforms } = recordingGroup(
      [0.5, 0.5, 0.5, 1],
      [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1],
    );
    const { deps } = fakeDeps([{ group, entities: [7] }]);
    const pane = createViewPane3D({ deps });
    pane.mount(document.createElement("div"), fakeCtx());
    pane.update({ kind: "model", url: "m.bos" });
    await settle();
    pane.update({ kind: "instances", data: colorSlice });
    expect(appliedTransforms.length).toBe(0);
    pane.destroy();
  });

  it("renders a boxes table via the rig, and clears on an empty one", async () => {
    const { deps, rig } = fakeDeps();
    const pane = createViewPane3D({ deps });
    pane.mount(document.createElement("div"), fakeCtx());
    const bounds: Array<["minX" | "minY" | "minZ" | "maxX" | "maxY" | "maxZ", "Number"]> = [
      ["minX", "Number"],
      ["minY", "Number"],
      ["minZ", "Number"],
      ["maxX", "Number"],
      ["maxY", "Number"],
      ["maxZ", "Number"],
    ];
    pane.update({ kind: "boxes", data: makeSlice(bounds, [[0, 0, 0, 2, 4, 6]]) });
    expect(rig().boxes.length).toBe(1);
    const { transforms, colors } = rig().boxes[0];
    expect([transforms[0], transforms[5], transforms[10]]).toEqual([2, 4, 6]);
    expect([transforms[12], transforms[13], transforms[14]]).toEqual([1, 2, 3]);
    const gray = Math.fround(0.7);
    expect([...colors]).toEqual([gray, gray, gray, 1]);
    expect(rig().renders).toBeGreaterThan(0);
    pane.update({ kind: "boxes", data: makeSlice(bounds, []) });
    expect(rig().boxClears).toBe(1);
    pane.destroy();
  });

  it("emits a selection event when the rig reports a pick", () => {
    const { deps, rig } = fakeDeps();
    const pane = createViewPane3D({ deps });
    const { events, handler } = collect();
    pane.onEvent(handler);
    pane.mount(document.createElement("div"), fakeCtx());
    rig().pick(42);
    rig().pick(null); // cleared selection: no event
    expect(events).toEqual([
      { kind: "selection", event: { source: "view3d", ids: ["42"] } },
    ]);
    pane.destroy();
  });

  it("mounts and destroys the real default rig headless (no WebGL attach)", () => {
    const warn = vi.spyOn(console, "error").mockImplementation(() => {});
    const host = document.createElement("div");
    const pane = createViewPane3D();
    pane.mount(host, fakeCtx());
    expect(host.querySelector("canvas.bof-panes-canvas")).not.toBeNull();
    pane.destroy();
    expect(host.childNodes.length).toBe(0);
    warn.mockRestore();
  });
});
