import { afterEach, describe, expect, it } from "vitest";
import { createViewer, defaultFeatures } from "@bim-open-toolkit/viewer";
import { translation } from "@bim-open-toolkit/model";
import { layoutsSlice } from "@bim-open-toolkit/features";
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
});
