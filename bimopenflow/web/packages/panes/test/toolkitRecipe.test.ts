import { afterEach, describe, expect, it, vi } from "vitest";
import { createViewer, defaultFeatures } from "@bim-open-toolkit/viewer";
import { translation } from "@bim-open-toolkit/model";
import { clippingSlice, layoutsSlice } from "@bim-open-toolkit/features";
import { fakeRenderer, testFrames } from "../../../../../viewer/packages/viewer/test/support/fake-renderer";
import { twoObjectModel } from "../../../../../viewer/packages/viewer/test/support/model-fixture";
import { mountRecipe, recipeFeatures, requireResult } from "../src/toolkitRecipe";
import { restoreSourceColors } from "../src/toolkitSource";
import type { ViewStep } from "../src/viewRecipe";

const disposers: (() => void)[] = [];
afterEach(() => disposers.splice(0).forEach(dispose => dispose()));

const scene: ViewStep = { operation: "scene", input: { path: "fixture.bos" } };
const categories: ViewStep = { operation: "categoryStyle", input: { opacity: 1 } };
const explode = (strength: number): ViewStep => ({ operation: "explode", input: { by: "category", strength } });
const band: ViewStep = { operation: "sectionRange", input: { axis: "x", range: [.3, .7] } };

function setup() {
  const loaded = twoObjectModel();
  // Like BOS, semantic records have identity transforms while placements are translated.
  const model = { ...loaded, data: { ...loaded.data, objects: loaded.data.objects.map(record => ({ ...record, transform: translation([0, 0, 0]) })) } };
  const clock = testFrames();
  const viewer = createViewer({ renderer: fakeRenderer, schedule: clock.schedule, features: [...defaultFeatures(), ...recipeFeatures] });
  viewer.views.all()[0]!.resize(800, 600);
  requireResult(viewer.show(model));
  requireResult(viewer.run("view.fit"));
  const table = viewer.binding.tableOf(model.data.ref.id)!;
  const restore = () => restoreSourceColors(model, table);
  restore();
  const recipe = mountRecipe(viewer, model.data, table, restore);
  disposers.push(() => { recipe.dispose(); viewer.dispose(); });
  const transforms = () => table.groups.map(group => Array.from(group.transforms));
  return { viewer, table, recipe, transforms, source: transforms(), camera: viewer.views.all()[0]!.camera() };
}

describe("recipe branch isolation with a real viewer binding", () => {
  it("changes category colors and legend together while retaining opacity", () => {
    const { recipe, table } = setup();
    recipe.apply([scene, { operation: "categoryStyle", input: { opacity: .4, palette: "classic" } }]);
    const original = table.groups.map(group => Array.from(group.colors));
    const legend = recipe.legend();
    recipe.apply([scene, { operation: "categoryStyle", input: { opacity: .4, palette: "pastel" } }]);
    expect(table.groups.map(group => Array.from(group.colors))).not.toEqual(original);
    expect(recipe.legend().map(entry => entry.color)).not.toEqual(legend.map(entry => entry.color));
    expect(recipe.legend().map(entry => [entry.name,entry.count])).toEqual(legend.map(entry => [entry.name,entry.count]));
    expect(Array.from(table.opacity).every(alpha => Math.abs(alpha - .4) < .00001)).toBe(true);
    recipe.apply([scene, { operation: "categoryStyle", input: { opacity: .4, palette: "classic" } }]);
    expect(table.groups.map(group => Array.from(group.colors))).toEqual(original);
  });
  it("restores category opacity after previewing a separate ghost branch", () => {
    const { recipe, table } = setup();
    recipe.apply([scene, categories]);
    const opaque = table.groups.map(group => Array.from(group.colors));
    recipe.apply([scene, { operation: "categoryStyle", input: { opacity: .22 } }]);
    expect(Array.from(table.opacity).every(alpha => alpha < .23)).toBe(true);
    recipe.apply([scene, categories]);
    expect(Array.from(table.opacity)).toEqual([1, 1]);
    expect(table.groups.map(group => Array.from(group.colors))).toEqual(opaque);
  });

  it("restores every source transform and publishes it when moving from explode to a sibling band", () => {
    const { recipe, viewer, table, transforms, source, camera } = setup();
    recipe.apply([scene, categories, explode(.8)]);
    expect(transforms()).not.toEqual(source);
    const versions = table.groups.map(group => group.transformsVersion);
    recipe.apply([scene, categories, band]);
    expect(transforms()).toEqual(source);
    expect(viewer.session.read(layoutsSlice).layout.kind).toBe("none");
    expect(table.groups.every((group, i) => group.transformsVersion > versions[i])).toBe(true);
    expect(viewer.views.all()[0]!.camera()).toEqual(camera);
  });

  it("scrubs explode back to zero without transform drift", () => {
    const { recipe, transforms, source } = setup();
    for (const strength of [.2, .8, .3, .9, 0]) recipe.apply([scene, categories, explode(strength)]);
    expect(transforms()).toEqual(source);
  });

  it("retains explosion when it is actually upstream of the band", () => {
    const { recipe, transforms, source } = setup();
    recipe.apply([scene, categories, explode(.8)]);
    const exploded = transforms();
    recipe.apply([scene, categories, explode(.8), band]);
    expect(transforms()).not.toEqual(source);
    expect(transforms()).toEqual(exploded);
  });

  it("scrubs upstream sections without restoring materials, moving the camera or replaying downstream controls", () => {
    const { recipe, viewer, table } = setup();
    const section = (fraction: number): ViewStep => ({ operation: "section", input: { axis: "x", fraction } });
    const projection: ViewStep = { operation: "projection", input: { mode: "orthographic" } };
    const environment: ViewStep = { operation: "environment", input: { theme: "dark", grid: true } };
    const steps = (fraction: number) => [scene, categories, section(fraction), projection, environment];
    const run = vi.spyOn(viewer, "run");
    const apply = vi.spyOn(viewer, "apply");
    recipe.apply(steps(.2));
    const fullReplayCommands = run.mock.calls.length;
    const camera = viewer.views.all()[0]!.camera();
    requireResult(viewer.run("view.look", { camera: { ...camera, camera: { ...camera.camera, position: [10, 12, 14] } } }));
    const navigated = viewer.views.all()[0]!.camera();
    const colors = table.groups.map(group => group.colorsVersion);
    run.mockClear();
    apply.mockClear();
    for (const fraction of [.3, .4, .5]) recipe.apply(steps(fraction));
    expect(run.mock.calls.map(call => call[0])).toEqual(Array(3).fill("clipping.sectionAt"));
    expect(fullReplayCommands).toBeGreaterThanOrEqual(10);
    expect(apply).not.toHaveBeenCalled();
    expect(table.groups.map(group => group.colorsVersion)).toEqual(colors);
    expect(viewer.views.all()[0]!.camera()).toEqual(navigated);
    const expected = setup();
    expected.recipe.apply(steps(.5));
    expect(viewer.session.read(clippingSlice)).toEqual(expected.viewer.session.read(clippingSlice));
  });

  it("keeps later clipping overrides when an earlier section changes", () => {
    const { recipe, viewer } = setup();
    const section = (fraction: number): ViewStep => ({ operation: "section", input: { axis: "z", fraction } });
    recipe.apply([scene, section(.1), band]);
    const clipping = viewer.session.read(clippingSlice);
    recipe.apply([scene, section(.9), band]);
    expect(viewer.session.read(clippingSlice)).toEqual(clipping);
  });

  it.each<ViewStep>([
    { operation: "scene", input: { path: "other.bos" } },
    { operation: "categoryStyle", input: { opacity: .3 } },
    { operation: "projection", input: { mode: "plan" } },
  ])("fully replays when a non-spatial step changes: $operation", replacement => {
    const { recipe, viewer } = setup();
    const steps: ViewStep[] = [scene, categories, band, { operation: "projection", input: { mode: "perspective" } }];
    recipe.apply(steps);
    const run = vi.spyOn(viewer, "run");
    recipe.apply(steps.map(step => step.operation === replacement.operation ? replacement : step));
    expect(run.mock.calls.map(call => call[0])).toContain("appearance.clear");
    expect(run.mock.calls.map(call => call[0])).toContain("layouts.reset");
  });

  it("scrubs an upstream explosion back to source without replaying a downstream section", () => {
    const { recipe, viewer, transforms, source } = setup();
    recipe.apply([scene, categories, explode(.8), band]);
    const clipping = viewer.session.read(clippingSlice);
    const run = vi.spyOn(viewer, "run");
    recipe.apply([scene, categories, explode(0), band]);
    expect(run.mock.calls.map(call => call[0])).toEqual(["layouts.explode"]);
    expect(transforms()).toEqual(source);
    expect(viewer.session.read(clippingSlice)).toEqual(clipping);
  });
});
