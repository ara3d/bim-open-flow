import { describe, it, expect } from "vitest";
import { emptyObject, instanceRecords, objectKey, objectRef, mesh, translation } from "@bim-open-toolkit/model";
import { loadedModel } from "@bim-open-toolkit/formats";
import { buildInstanceTable } from "@bim-open-toolkit/render";
import { restoreSourceColors, visibleSource } from "../src/toolkitSource";
import { layoutModel, requireResult } from "../src/toolkitRecipe";

describe("BIM Flow source adapter", () => {
  it("excludes source-hidden outliers without deleting identities or changing source arrays", () => {
    const ref = {id:"m",revision:"r"};
    const data = {ref,coordinates:{units:"metres" as const,up:"z" as const,registration:{kind:"local" as const}},
      objects:[emptyObject(objectRef(ref,"bos:7")),emptyObject(objectRef(ref,"bos:8"))]};
    const triangle = mesh(new Float32Array([0,0,0,1,0,0,0,1,0]),new Uint32Array([0,1,2]));
    const original = loadedModel("bos", data, {meshes:[triangle],instances:instanceRecords([
      {meshIndex:0,objectIndex:0,transform:translation([4,5,6]),color:[1,0,0],opacity:.4,visible:true},
      {meshIndex:0,objectIndex:1,transform:translation([10000,0,0]),color:[0,1,0],opacity:1,visible:false},
    ])},0);
    const visible = visibleSource(original);
    expect([...original.geometry.instances.meshIndex]).toEqual([0,0]);
    expect([...visible.geometry.instances.meshIndex]).toEqual([0,-1]);
    expect(visible.data).toBe(original.data);
    const table = requireResult(buildInstanceTable(visible.geometry, data.objects.map(o=>objectKey(o.ref))));
    expect(table.rowCount).toBe(1);
    table.groups[0]!.setColors(0,new Float32Array([0,0,1,1]));
    restoreSourceColors(visible,table);
    expect([...table.colors[0]!]).toEqual([1,0,0,Math.fround(.4)]);
    expect(layoutModel(data,table).objects[0]!.transform.slice(12,15)).toEqual([4,5,6]);
  });
});
