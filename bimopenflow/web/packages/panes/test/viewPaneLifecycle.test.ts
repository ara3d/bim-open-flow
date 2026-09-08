import { describe, it, expect, vi } from "vitest";
import { createViewPane3D } from "../src/viewPane3D";
import type { ViewerRig } from "../src/viewerDeps";
import type { GroupEntityMap } from "../src/instanceTable";
import { fakeCtx, settle, makeSlice } from "./helpers";

describe("3D pane lifecycle", () => {
  const make = () => {
    const loads: {resolve:(maps:readonly GroupEntityMap[])=>void;reject:(error:Error)=>void}[] = [];
    const rig: ViewerRig = {
      load: () => new Promise((resolve,reject)=>loads.push({resolve,reject})),
      setBoxes:vi.fn(),clearBoxes:vi.fn(),requestRender:vi.fn(),dispose:vi.fn(),applyRecipe:vi.fn(),
    };
    const pane=createViewPane3D({deps:{createRig:()=>rig}});
    const event=vi.fn();
    pane.onEvent(event);
    const root=document.createElement("div");
    pane.mount(root,fakeCtx());
    return {pane,rig,loads,event,root};
  };
  it("suppresses obsolete load errors after switching model", async () => {
    const {pane,loads,event,root}=make();
    pane.update({kind:"model",url:"a.bos"});
    pane.update({kind:"model",url:"b.bos"});
    loads[1]!.resolve([]);
    await settle();
    loads[0]!.reject(new Error("obsolete"));
    await settle();
    expect(event.mock.calls).toHaveLength(1);
    expect(event.mock.calls[0]![0].payload.url).toBe("b.bos");
    expect(root.querySelector('[role="alert"]')).toBeNull();
    pane.destroy();
  });
  it("never emits completion after disposal", async () => {
    const {pane,loads,event,rig}=make();
    pane.update({kind:"model",url:"a.bos"});
    pane.destroy();
    loads[0]!.resolve([]);
    await settle();
    expect(event).not.toHaveBeenCalled();
    expect(rig.dispose).toHaveBeenCalledTimes(1);
  });
  it("applies a queued complete recipe after loading", async () => {
    const {pane,loads,rig}=make();
    const data=makeSlice([["operation","Text"],["input","Text"]],[["scene",'{"path":"a.bos"}'],["section",'{"axis":"z","fraction":0.5}']]);
    pane.update({kind:"model",url:"a.bos"});
    pane.update({kind:"view",data});
    expect(rig.applyRecipe).not.toHaveBeenCalled();
    loads[0]!.resolve([]);
    await settle();
    expect(rig.applyRecipe).toHaveBeenCalledWith(data);
    pane.destroy();
  });
});
