import type { NodeDescriptor, TableSlice } from "@bimopenflow/contracts";
import { parsePortRef, type GraphDocument } from "@bimopenflow/state";
import { parseViewRecipe } from "../../panes/src/viewRecipe";

export type LiveViewRecipe =
  | { kind: "ready"; data: TableSlice }
  | { kind: "unsupported" }
  | { kind: "invalid"; message: string };

const operations = new Set(["scene", "section", "sectionBox", "explode", "projection", "environment", "categoryStyle", "tint", "sectionRange"]);

/** Preview only the explicit, bounded view chain. General graph evaluation stays on the host. */
export function buildLiveViewRecipe(document: GraphDocument, nodeId: string, catalog: ReadonlyMap<string, NodeDescriptor>): LiveViewRecipe {
  const nodes = new Map(document.structure.nodes.map(node => [node.id, node]));
  const visited = new Set<string>();
  const rows: unknown[][] = [];
  try {
    let current = nodeId;
    while (true) {
      if (visited.has(current)) throw new Error("The view graph contains a cycle.");
      if (rows.length >= 64) throw new Error("A view recipe may contain at most 64 steps.");
      visited.add(current);
      const node = nodes.get(current);
      if (!node) throw new Error(`Missing view node '${current}'.`);
      const operation = node.kind.startsWith("view3d.") ? node.kind.slice(7) : "";
      const descriptor = catalog.get(node.kind);
      if (!operations.has(operation) || node.version !== 1 || descriptor?.version !== 1)
        return { kind: "unsupported" };
      const read = (name: string): string => {
        const value = document.values[current]?.[name] ?? descriptor.params.find(p => p.name === name)?.default;
        if (value === undefined) throw new Error(`Missing parameter '${current}.${name}'.`);
        return value;
      };
      const numeric = (name: string): number => {
        const value = read(name).trim();
        // Canonical decimal numbers only: Number("") and Number("0xff") accept values the host does not.
        if (!/^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:e[+-]?\d+)?$/i.test(value) || !Number.isFinite(Number(value)))
          throw new Error(`'${current}.${name}' must be a finite number.`);
        return Number(value);
      };
      let input: object;
      switch (operation) {
        case "scene": input = { path: read("path") }; break;
        case "section": input = { axis: read("axis"), fraction: numeric("fraction") }; break;
        case "sectionBox": input = { fraction: numeric("fraction") }; break;
        case "explode": input = { by: "category", strength: numeric("strength") }; break;
        case "projection": input = { mode: read("mode") }; break;
        case "categoryStyle": input = { opacity: numeric("opacity") }; break;
        case "tint": input = { color: read("color"), opacity: numeric("opacityPercent") / 100 }; break;
        case "sectionRange": input = { axis: read("axis"), range: JSON.parse(read("range")) }; break;
        case "environment": {
          const grid = read("grid").trim().toLowerCase();
          if (grid !== "true" && grid !== "false") throw new Error(`'${current}.grid' must be true or false.`);
          input = { theme: read("theme"), grid: grid === "true" };
          break;
        }
        default: return { kind: "unsupported" };
      }
      rows.push([operation, JSON.stringify(input)]);
      const incoming = document.structure.edges.filter(edge => edge.to.startsWith(`${current}.`));
      if (operation === "scene") {
        if (incoming.length) throw new Error("A scene is a source and cannot have an incoming connection.");
        break;
      }
      if (incoming.length !== 1 || incoming[0].to !== `${current}.view`)
        throw new Error(`Connect exactly one view output to '${current}.view'.`);
      const source = parsePortRef(incoming[0].from);
      if (source.port !== "view") return { kind: "unsupported" };
      current = source.nodeId;
    }
    const data: TableSlice = {
      columns: [{ name: "operation", type: "Text" }, { name: "input", type: "Text" }],
      rows: rows.reverse(), totalRows: rows.length, skip: 0,
    };
    parseViewRecipe(data);
    return { kind: "ready", data };
  } catch (error) {
    return { kind: "invalid", message: error instanceof Error ? error.message : String(error) };
  }
}
