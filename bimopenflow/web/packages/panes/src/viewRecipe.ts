import type { TableSlice } from "@bimopenflow/contracts";
import { boolean, literal, number, object, string, union, type Bounds, type Schema } from "@bim-open-toolkit/model";

export type ViewStep =
  | { operation: "scene"; input: { path: string } }
  | { operation: "section"; input: { axis: "x" | "y" | "z"; fraction: number } }
  | { operation: "sectionBox"; input: { fraction: number } }
  | { operation: "explode"; input: { by: "category"; strength: number } }
  | { operation: "projection"; input: { mode: "perspective" | "orthographic" | "plan" } }
  | { operation: "environment"; input: { theme: "light" | "dark"; grid: boolean } }
  | { operation: "categoryStyle"; input: { opacity: number } };

const stepSchema: Schema<ViewStep> = union<ViewStep>(
  object({ operation: literal("scene"), input: object({ path: string() }) }),
  object({ operation: literal("section"), input: object({ axis: union(literal("x"), literal("y"), literal("z")), fraction: number() }) }),
  object({ operation: literal("sectionBox"), input: object({ fraction: number() }) }),
  object({ operation: literal("explode"), input: object({ by: literal("category"), strength: number() }) }),
  object({ operation: literal("projection"), input: object({ mode: union(literal("perspective"), literal("orthographic"), literal("plan")) }) }),
  object({ operation: literal("environment"), input: object({ theme: union(literal("light"), literal("dark")), grid: boolean() }) }),
  object({ operation: literal("categoryStyle"), input: object({ opacity: number() }) }),
);

/** Validate the complete recipe before changing the view. No arbitrary command dispatch. */
export function parseViewRecipe(table: TableSlice): readonly ViewStep[] {
  const op = table.columns.findIndex(c => c.name === "operation");
  const args = table.columns.findIndex(c => c.name === "input");
  if (op < 0 || args < 0 || table.rows.length === 0 || table.rows.length > 64 || table.rows.length !== table.totalRows)
    throw new Error("Expected a complete view recipe of 1–64 steps.");
  return table.rows.map((row, index) => {
    const value: unknown = JSON.parse(String(row[args]));
    const result = stepSchema.check({ operation: row[op], input: value }, []);
    if (!result.ok) throw new Error(`Invalid view step ${index + 1}: ${result.diagnostics.map(d => d.message).join("; ")}`);
    const step = result.value;
    if ((index === 0) !== (step.operation === "scene")) throw new Error("A recipe must start with exactly one scene step.");
    if (step.operation === "section" || step.operation === "sectionBox") {
      const min = step.operation === "sectionBox" ? .01 : 0;
      if (step.input.fraction < min || step.input.fraction > 1) throw new Error("Section fraction is out of range.");
    }
    if (step.operation === "explode" && (step.input.strength < 0 || step.input.strength > 5)) throw new Error("Explode strength is out of range.");
    if (step.operation === "categoryStyle" && (step.input.opacity < 0 || step.input.opacity > 1)) throw new Error("Opacity is out of range.");
    return step;
  });
}

/** Fractions use original model bounds, unaffected by an exploded presentation. */
export function sectionElevation(bounds: Bounds, axis: "x" | "y" | "z", fraction: number): number {
  const i = axis === "x" ? 0 : axis === "y" ? 1 : 2;
  return bounds.min[i] + (bounds.max[i] - bounds.min[i]) * fraction;
}
