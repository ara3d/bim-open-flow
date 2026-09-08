import { describe, it, expect } from "vitest";
import { parseViewRecipe, sectionElevation } from "../src/viewRecipe";
import { categoryRules } from "../src/toolkitRecipe";
import { emptyObject, objectRef } from "@bim-open-toolkit/model";
import { makeSlice } from "./helpers";
const table = (steps: readonly (readonly [string, unknown])[]) => makeSlice([["operation","Text"],["input","Text"]], steps.map(([op, input]) => [op, JSON.stringify(input)]));
describe("view recipes", () => {
  it("keeps unknown categories separate and counts source objects once", () => {
    const ref = {id:"m",revision:"r"};
    const rules = categoryRules({ref,coordinates:{units:"metres",up:"z",registration:{kind:"local"}},
      objects:[{...emptyObject(objectRef(ref,"a")),category:"Doors"},emptyObject(objectRef(ref,"b"))]}, .2);
    expect(rules).toHaveLength(2);
    expect(rules.find(r=>r.name==="Unknown category")?.change.color).toEqual([.5,.5,.5]);
    expect(rules.every(r=>r.change.opacity===.2)).toBe(true);
  });
  it("reads a composable cutaway and plan recipe", () => {
    const result = parseViewRecipe(table([["scene",{path:"Snowdon.bos"}],["section",{axis:"z",fraction:.5}],["projection",{mode:"plan"}]]));
    expect(result.map(s => s.operation)).toEqual(["scene","section","projection"]);
    expect(sectionElevation({min:[-10,0,12],max:[10,30,52]}, "z", .5)).toBe(32);
  });
  const invalidRecipes: Array<Array<[string, unknown]>> = [
    [["view.fit",{}]],
    [["scene",{path:"x"}],["scene",{path:"y"}]],
    [["scene",{path:"x"}],["section",{axis:"z",fraction:2}]],
    [["scene",{path:"x"}],["sectionBox",{fraction:0}]],
    [["scene",{path:"x"}],["projection",{mode:"invalid"}]],
  ];
  it.each(invalidRecipes.map(steps => ({ steps })))("rejects unsupported commands and invalid recipes", ({ steps }) => {
    expect(() => parseViewRecipe(table(steps))).toThrow();
  });
  it("rejects truncated recipe pages before dispatch", () => {
    const data = table([["scene",{path:"x"}]]);
    expect(() => parseViewRecipe({...data,totalRows:2})).toThrow("complete");
  });
});
