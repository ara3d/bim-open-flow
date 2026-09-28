import { describe, expect, it } from "vitest";
import type { NodeDescriptor, NodeState, ParamKind } from "@bimopenflow/contracts";
import { initialState, type State } from "@bimopenflow/state";
import { NO_PORT_RESULTS, type PortResultsView } from "@bimopenflow/graph";
import { createStepList, stepListModel, type Step } from "../src/stepList.js";

const desc = (kind: string, params: [string, ParamKind][] = []): NodeDescriptor => ({
  kind,
  version: 1,
  capability: "Pure",
  inputs: [{ name: "input", type: "Table", optional: true }],
  outputs: [
    { name: "count", type: "Integer", optional: false },
    { name: "table", type: "Table", optional: false },
  ],
  params: params.map(([name, kind]) => ({ name, kind, default: "" })),
  description: kind,
});

const catalog = new Map<string, NodeDescriptor>([
  ["duck.query", desc("duck.query", [["database", "FilePath"], ["sql", "Text"]])],
  ["table.join", desc("table.join", [["on", "Text"]])],
  ["table.sort", desc("table.sort", [["column", "Text"], ["limit", "Integer"]])],
]);

const ok = (nodeId: string): NodeState => ({ nodeId, status: "Ok", warnings: [] });

// The plan's worked example: doors -> join <- storeys, join -> answer.
const exampleState = (patch: Partial<State> = {}): State => ({
  ...initialState,
  document: {
    formatVersion: "0.1.0",
    structure: {
      nodes: [
        { id: "answer", kind: "table.sort", version: 1 },
        { id: "join", kind: "table.join", version: 1 },
        { id: "storeys", kind: "duck.query", version: 1 },
        { id: "doors", kind: "duck.query", version: 1 },
      ],
      edges: [
        { from: "storeys.table", to: "join.input" },
        { from: "doors.table", to: "join.input" },
        { from: "join.table", to: "answer.input" },
      ],
    },
    values: {
      doors: { database: "C:\\Users\\me\\data\\snowdon.duckdb", sql: "select * from doors" },
      join: {},
      answer: { limit: "10", column: "Level", descendingcolumn: "true" },
    },
    layout: {
      doors: { x: 0, y: 0 },
      storeys: { x: 0, y: 200 },
      join: { x: 300, y: 100 },
      answer: { x: 600, y: 100 },
    },
  },
  ...patch,
});

describe("stepListModel", () => {
  it("numbers the worked example in dataflow order and names feeders only on the join", () => {
    const steps = stepListModel(exampleState(), catalog, NO_PORT_RESULTS);
    expect(steps.map((s) => [s.index, s.nodeId, s.from])).toEqual([
      [1, "doors", []],
      [2, "storeys", []],
      [3, "join", [1, 2]],
      [4, "answer", []],
    ]);
    expect(steps[0]!.title).toBe("duck.query");
    expect(steps[0]!.kind).toBe("duck.query");
  });

  it("summarises set parameters in catalog order, shortening a FilePath to its file name", () => {
    const steps = stepListModel(exampleState(), catalog, NO_PORT_RESULTS);
    expect(steps[0]!.summary).toBe("database = snowdon.duckdb · sql = select * from doors");
    expect(steps[1]!.summary).toBe("");
    expect(steps[2]!.summary).toBe("");
    expect(steps[3]!.summary).toBe("column = Level · limit = 10");
  });

  it("truncates a long value to 40 characters with an ellipsis", () => {
    const state = exampleState();
    const long = "select name, width, height from doors where width is not null";
    const values = { ...state.document.values, doors: { sql: long } };
    const steps = stepListModel({ ...state, document: { ...state.document, values } }, catalog, NO_PORT_RESULTS);
    const shown = steps[0]!.summary.slice("sql = ".length);
    expect(shown).toHaveLength(40);
    expect(shown.endsWith("…")).toBe(true);
    expect(long.startsWith(shown.slice(0, 39))).toBe(true);
  });

  it("lists a kind missing from the catalog with its stored values", () => {
    const steps = stepListModel(exampleState(), new Map(), NO_PORT_RESULTS);
    expect(steps[3]!.summary).toBe("limit = 10 · column = Level · descendingcolumn = true");
  });

  it("shows a row count only for a first table output the results view has counted", () => {
    const results: PortResultsView = {
      counts: new Map([
        ["doors.table", { rows: 142, current: true }],
        ["storeys.count", { rows: 7, current: true }],
      ]),
      peek: null,
    };
    const steps = stepListModel(exampleState(), catalog, results);
    expect(steps.map((s) => s.rows)).toEqual([142, undefined, undefined, undefined]);
  });

  it("carries status and badge text, and marks the selected step", () => {
    const state = exampleState({
      evalState: { doors: ok("doors"), join: { nodeId: "join", status: "Unready", warnings: [] } },
      selection: ["join"],
    });
    const steps = stepListModel(state, catalog, NO_PORT_RESULTS);
    expect(steps[0]).toMatchObject({ status: "Ok", badge: "Ok", selected: false });
    expect(steps[1]!.status).toBeUndefined();
    expect(steps[2]).toMatchObject({ status: "Unready", badge: "Needs setup", selected: true });
  });
});

describe("createStepList", () => {
  const step = (patch: Partial<Step> = {}): Step => ({
    index: 1,
    nodeId: "doors",
    title: "duck.query",
    kind: "duck.query",
    summary: "",
    from: [],
    selected: false,
    ...patch,
  });

  const mount = () => {
    const host = document.createElement("div");
    const selected: string[] = [];
    const list = createStepList(host, { onSelect: (id) => selected.push(id) });
    return { host, selected, list };
  };

  it("paints each step with its number, title, summary, and meta line", () => {
    const { host, list } = mount();
    list.render([
      step({ summary: "sql = select 1", status: "Ok", badge: "Ok", rows: 1420 }),
      step({ index: 2, nodeId: "join", title: "table.join", kind: "table.join", from: [1, 2] }),
    ]);
    const buttons = host.querySelectorAll(".bof-app-steps-step");
    expect(buttons).toHaveLength(2);
    expect(buttons[0]!.querySelector(".bof-app-steps-index")!.textContent).toBe("1.");
    expect(buttons[0]!.querySelector(".bof-app-steps-summary")!.textContent).toBe("sql = select 1");
    expect(buttons[0]!.querySelector(".bof-app-steps-meta")!.textContent).toBe("Ok · 1,420 rows");
    expect(buttons[0]!.querySelector(".bof-app-steps-status-Ok")).not.toBeNull();
    expect(buttons[1]!.querySelector(".bof-app-steps-summary")).toBeNull();
    expect(buttons[1]!.querySelector(".bof-app-steps-meta")!.textContent).toBe("from 1, 2");
  });

  it("omits the row count when the step has none", () => {
    const { host, list } = mount();
    list.render([step({ status: "Ok", badge: "Ok" })]);
    expect(host.querySelector(".bof-app-steps-rows")).toBeNull();
  });

  it("highlights the selected step", () => {
    const { host, list } = mount();
    list.render([step(), step({ index: 2, nodeId: "join", selected: true })]);
    const selected = host.querySelectorAll(".bof-app-steps-selected");
    expect(selected).toHaveLength(1);
    expect((selected[0] as HTMLElement).dataset.nodeId).toBe("join");
    expect(selected[0]!.getAttribute("aria-current")).toBe("step");
  });

  it("calls onSelect with the node id of the clicked step", () => {
    const { host, list, selected } = mount();
    list.render([step(), step({ index: 2, nodeId: "join" })]);
    (host.querySelectorAll(".bof-app-steps-step")[1] as HTMLElement).click();
    expect(selected).toEqual(["join"]);
  });

  it("re-renders in place, says so when the flow is empty, and removes itself on dispose", () => {
    const { host, list } = mount();
    list.render([step()]);
    list.render([]);
    expect(host.querySelectorAll(".bof-app-steps-step")).toHaveLength(0);
    expect(host.querySelector(".bof-app-steps-empty")!.textContent).toBe("No steps in this flow yet");
    list.dispose();
    expect(host.children).toHaveLength(0);
    expect(document.getElementById("bof-app-steps-styles")).not.toBeNull();
  });
});
