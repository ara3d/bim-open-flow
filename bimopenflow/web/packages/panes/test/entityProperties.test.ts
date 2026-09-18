import { describe, expect, it } from "vitest";
import type { EntityParameter, EntityProperties } from "@bimopenflow/contracts";
import {
  groupParameters,
  parameterText,
  renderEntityMessage,
  renderEntityProperties,
} from "../src/entityProperties";

const param = (group: string, name: string, value: string, units?: string): EntityParameter =>
  ({ group, name, value, units });

describe("groupParameters", () => {
  it("collects consecutive runs of the same group in host order", () => {
    const groups = groupParameters([
      param("A", "a1", "1"),
      param("A", "a2", "2"),
      param("B", "b1", "3"),
    ]);
    expect(groups.map((g) => g.name)).toEqual(["A", "B"]);
    expect(groups[0].parameters.map((p) => p.name)).toEqual(["a1", "a2"]);
  });

  it("returns nothing for no parameters", () => {
    expect(groupParameters([])).toEqual([]);
  });
});

describe("parameterText", () => {
  it("appends units when the host gives them", () => {
    expect(parameterText(param("A", "a", "12.5", "kg"))).toBe("12.5 kg");
    expect(parameterText(param("A", "a", "12.5"))).toBe("12.5");
  });
});

describe("renderEntityProperties", () => {
  const entity: EntityProperties = {
    localId: 1234,
    globalId: "0Xs3",
    name: "Basic Wall",
    category: "IFCWALLSTANDARDCASE",
    parameters: [
      param("Pset_NRCOperationalCarbon", "OperationalCarbon_kgCO2e_per_year", "412.5", "kgCO2e"),
      param("Pset_WallCommon", "IsExternal", "true"),
    ],
  };

  it("renders identity then one section per group", () => {
    const root = document.createElement("div");
    renderEntityProperties(root, entity);
    expect(root.hidden).toBe(false);
    expect(root.querySelector(".bof-panes-title")?.textContent).toBe("Basic Wall");
    const sections = [...root.querySelectorAll(".bof-panes-section")].map((e) => e.textContent);
    expect(sections).toEqual(["Pset_NRCOperationalCarbon", "Pset_WallCommon"]);
    expect(root.textContent).toContain("IFCWALLSTANDARDCASE");
    expect(root.textContent).toContain("0Xs3");
    expect(root.textContent).toContain("412.5 kgCO2e");
  });

  it("falls back to the local id when the entity has no name, and replaces prior content", () => {
    const root = document.createElement("div");
    renderEntityProperties(root, entity);
    renderEntityProperties(root, { ...entity, name: undefined, parameters: [] });
    expect(root.querySelector(".bof-panes-title")?.textContent).toBe("Entity 1234");
    expect(root.querySelectorAll(".bof-panes-section")).toHaveLength(0);
  });
});

describe("renderEntityMessage", () => {
  it("shows one error line", () => {
    const root = document.createElement("div");
    root.hidden = true;
    renderEntityMessage(root, "No properties");
    expect(root.hidden).toBe(false);
    expect(root.querySelector(".bof-panes-error")?.textContent).toBe("No properties");
  });
});
