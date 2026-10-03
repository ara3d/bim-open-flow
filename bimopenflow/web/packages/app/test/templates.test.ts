import { describe, expect, it } from "vitest";
import { groupTemplates, type FlowTemplate } from "../src/templates.js";

const t = (id: string, folder: string): FlowTemplate => ({
  id, folder, title: id, description: "", nodeCount: 1, kinds: ["table.sort"],
});

describe("groupTemplates", () => {
  it("orders known folders by the fixed order and unknown ones after by name", () => {
    const groups = groupTemplates(
      [t("z", "zeta-analyses"), t("d", "duckdb-analyses"), t("a", "analyses"), t("b", "bim-analyses"), t("y", "alpha-analyses")],
      new Set(),
    );
    expect(groups.map((g) => g.folder)).toEqual(["analyses", "bim-analyses", "duckdb-analyses", "alpha-analyses", "zeta-analyses"]);
    expect(groups.map((g) => g.label)).toEqual(["Tables", "BIM", "DuckDB studio", "alpha-analyses", "zeta-analyses"]);
  });

  it("keeps each folder's template order and marks presence", () => {
    const [g] = groupTemplates([t("b", "nrc-analyses"), t("a", "nrc-analyses")], new Set(["a"]));
    expect(g!.label).toBe("NRC paper");
    expect(g!.templates.map((x) => [x.id, x.present])).toEqual([["b", false], ["a", true]]);
  });
});
