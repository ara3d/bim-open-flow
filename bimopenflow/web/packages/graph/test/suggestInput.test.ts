import { describe, expect, it } from "vitest";
import type { SuggestionList } from "@bimopenflow/contracts";
import { attachSuggestions } from "../src/suggestInput.js";

const okList = (...values: string[]): SuggestionList => ({
  status: "Ok",
  values: values.map((value) => ({ value })),
});

const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

function makeInput() {
  const input = document.createElement("input");
  input.type = "text";
  document.body.appendChild(input);
  return input;
}

describe("attachSuggestions", () => {
  it("fills the datalist from the fetch on focus", async () => {
    const input = makeInput();
    const detach = attachSuggestions(input, "list1", async () => okList("name", "count"));
    expect(input.getAttribute("list")).toBe("list1");
    input.dispatchEvent(new Event("focus"));
    await flush();
    const options = [...document.getElementById("list1")!.children].map(
      (o) => (o as HTMLOptionElement).value,
    );
    expect(options).toEqual(["name", "count"]);
    detach();
  });

  it("token-completes options as the user types after a comma", async () => {
    const input = makeInput();
    const detach = attachSuggestions(input, "list2", async () => okList("name", "count"));
    input.dispatchEvent(new Event("focus"));
    await flush();
    input.value = "name, ";
    input.dispatchEvent(new Event("input"));
    const options = [...document.getElementById("list2")!.children].map(
      (o) => (o as HTMLOptionElement).value,
    );
    expect(options).toEqual(["name, name", "name, count"]);
    detach();
  });

  it("puts the reason in the tooltip when suggestions are unready", async () => {
    const input = makeInput();
    const detach = attachSuggestions(input, "list3", async () => ({
      status: "Unready",
      values: [],
      reason: "Connect a table to 'table' to see columns",
    }));
    input.dispatchEvent(new Event("focus"));
    await flush();
    expect(input.title).toContain("Connect a table");
    expect(document.getElementById("list3")!.children.length).toBe(0);
    detach();
  });

  it("survives a fetch error and cleans up on detach", async () => {
    const input = makeInput();
    const detach = attachSuggestions(input, "list4", async () => {
      throw new Error("offline");
    });
    input.dispatchEvent(new Event("focus"));
    await flush();
    expect(document.getElementById("list4")!.children.length).toBe(0);
    detach();
    expect(document.getElementById("list4")).toBeNull();
    expect(input.getAttribute("list")).toBeNull();
  });
});
