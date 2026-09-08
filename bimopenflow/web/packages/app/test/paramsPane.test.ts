import { describe, it, expect } from "vitest";
import { createParamsPane } from "../src/paramsPane";
import type { PaneEvent } from "@bimopenflow/panes";

describe("bounded Params controls", () => {
  it("keeps a percentage band ordered and bounded through typed edits", () => {
    const root = document.createElement("div"), pane = createParamsPane(), events: PaneEvent[] = [];
    pane.mount(root,{resolveAsset: url => url, requestTable: async () => { throw new Error("No table requests expected"); }});
    pane.onEvent(event => events.push(event));
    pane.update({kind:"inspect",node:{kind:"view3d.sectionRange",version:1,capability:"Pure",description:"",inputs:[],outputs:[],params:[
      {name:"range",kind:"Text",default:"[0.3,0.7]",control:{kind:"range",min:0,max:1,step:.01,unit:"percent"}},
    ]},values:{range:"[0.3,0.7]"}});
    const [start,end] = root.querySelectorAll("input");
    expect(start!.value).toBe("30");
    start!.value = "150"; start!.dispatchEvent(new Event("change"));
    expect(start!.value).toBe("70");
    end!.value = "-20"; end!.dispatchEvent(new Event("change"));
    expect(end!.value).toBe("70");
    expect(events.at(-1)).toMatchObject({payload:{value:"[0.7,0.7]"}});
    start!.value = ""; start!.dispatchEvent(new Event("change"));
    expect(start!.value).toBe("70");
    expect(events).toHaveLength(2);
    pane.destroy();
  });
});
