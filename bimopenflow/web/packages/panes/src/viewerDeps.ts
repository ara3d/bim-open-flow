// The thin, real wiring between the 3D pane and the viewer workspace
// (@ara3d/viewer-core/-loaders/-controls). Everything testable lives in
// instanceTable.ts; this module is deliberately minimal glue.
import { InstancedGroup, Viewer } from "@ara3d/viewer-core";
import { loadBos, loadGlb } from "@ara3d/viewer-loaders";
import {
  OrbitControls,
  Picker,
  PickControls,
  Selection,
  type InputElement,
  type PickElement,
} from "@ara3d/viewer-controls";
import type { ModelFormat } from "./pane";
import type { GroupEntityMap } from "./instanceTable";
import { UNIT_CUBE } from "./boxTable";

/** A mounted viewer with controls; what the 3D pane drives. */
export interface ViewerRig {
  /** Loads a model into the scene; resolves to the group→entity mapping (empty for GLB). */
  load(url: string, format: ModelFormat): Promise<readonly GroupEntityMap[]>;
  /** Replaces the boxes group: unit-cube instances (16 floats transform, RGBA color each). */
  setBoxes(transforms: Float32Array, colors: Float32Array): void;
  /** Removes the boxes group, if any. */
  clearBoxes(): void;
  requestRender(): void;
  dispose(): void;
}

export interface View3DDeps {
  createRig(
    canvas: HTMLCanvasElement,
    onPick: (entityId: number | null) => void,
  ): ViewerRig;
}

const hasWebGl = (canvas: HTMLCanvasElement): boolean => {
  try {
    return canvas.getContext("webgl2") !== null || canvas.getContext("webgl") !== null;
  } catch {
    return false;
  }
};

/**
 * Real rig: Viewer + OrbitControls + Picker/PickControls on the canvas.
 * Without a WebGL context (e.g. jsdom) the renderer is never attached; the
 * scene, controls, and picking wiring still exist.
 */
export const defaultView3DDeps: View3DDeps = {
  createRig(canvas, onPick) {
    const viewer = new Viewer();
    if (hasWebGl(canvas)) {
      viewer.attach(canvas);
      viewer.start();
    }
    const orbit = new OrbitControls({
      camera: viewer.camera,
      requestRender: () => viewer.requestRender(),
    });
    // DOM listener signatures are wider than the controls' minimal element
    // interfaces; the casts are safe for real elements.
    orbit.attach(canvas as unknown as InputElement);
    const selection = new Selection();
    const picker = new Picker(viewer.scene, viewer.objects);
    const picks = new PickControls(picker, selection, () => viewer.camera);
    picks.attach(canvas as unknown as PickElement);

    let maps: readonly GroupEntityMap[] = [];
    let boxes: InstancedGroup | null = null;
    selection.changed.on((s) => {
      const entity = s
        ? maps.find((m) => m.group === s.group)?.entities[s.instanceIndex]
        : undefined;
      onPick(entity ?? null);
    });

    return {
      async load(url, format) {
        if (format === "bos") {
          const result = await loadBos(url, viewer.scene);
          maps = result.groupEntities;
        } else {
          await loadGlb(url, viewer.scene);
          maps = []; // GLB carries no entity mapping: picks emit nothing
        }
        viewer.requestRender();
        return maps;
      },
      setBoxes(transforms, colors) {
        if (boxes) viewer.scene.removeGroup(boxes);
        boxes = new InstancedGroup(UNIT_CUBE);
        boxes.append(transforms, colors);
        viewer.scene.addGroup(boxes);
        viewer.requestRender();
      },
      clearBoxes() {
        if (!boxes) return;
        viewer.scene.removeGroup(boxes);
        boxes = null;
        viewer.requestRender();
      },
      requestRender: () => viewer.requestRender(),
      dispose() {
        picks.dispose();
        orbit.dispose();
        viewer.dispose();
      },
    };
  },
};
