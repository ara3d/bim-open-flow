import { describe,it,expect } from "vitest";
import { emptyDocument } from "@bimopenflow/state";
import { nodeTitle,previewAfterEdit,upstreamIds } from "../src/graphPreview";
const graph={...emptyDocument,structure:{nodes:[],edges:[{from:"model.view",to:"color.view"},{from:"color.view",to:"section.view"},{from:"section.view",to:"projection.view"},{from:"color.view",to:"explode.view"}]}};
describe("graph preview semantics",()=>{
  it("retains a downstream preview for edits in its chain",()=>{
    expect(previewAfterEdit(graph,"color","projection")).toBe("projection");
    expect([...upstreamIds(graph,"projection")]).toEqual(["projection","section","color","model"]);
  });
  it("switches to a separately edited branch",()=>{
    expect(previewAfterEdit(graph,"explode","projection")).toBe("explode");
  });
  it("names operations independently of parameter settings",()=>{
    expect(nodeTitle("view3d.projection")).toBe("Projection");
    expect(nodeTitle("view3d.sectionRange")).toBe("Section band");
  });
});
