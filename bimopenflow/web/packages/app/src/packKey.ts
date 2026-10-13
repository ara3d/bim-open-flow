// The canvas key: what the card border hues, the hollow wire, and the writer
// glyph mean. Six hues with no key teach nothing to a first-time viewer; the
// key reads the active canvas theme (canvasColors) so it lists exactly the
// roles that theme colours, and the two marks every theme draws.
// `packColor` gives the sidebar and the palette the same hue per pack.

import { canvasColors, css, PACK_ROLE_LABEL, packRole, packRoles, type PackRole } from "@bimopenflow/graph";

/** One row of the key: a swatch and what it means. */
export type KeyEntry =
  | { readonly swatch: "border"; readonly role: PackRole; readonly color: string; readonly label: string }
  | { readonly swatch: "relation"; readonly label: string }
  | { readonly swatch: "writer"; readonly label: string };

export const RELATION_LABEL = "A hollow wire carries a relation: pairs of rows, not a table";
export const WRITER_LABEL = "Writes a file, but only on Run";

/** The rows the key shows under the active canvas theme: a border swatch per
 *  role the theme colours, in packRoles order, then the two marks. */
export function keyEntries(): KeyEntry[] {
  const roles = canvasColors().roleAccents;
  const borders = packRoles.flatMap((role): KeyEntry[] => {
    const color = roles[role];
    return color ? [{ swatch: "border", role, color: css(color), label: PACK_ROLE_LABEL[role] }] : [];
  });
  return [...borders, { swatch: "relation", label: RELATION_LABEL }, { swatch: "writer", label: WRITER_LABEL }];
}

/** The CSS colour of `pack`'s border under the active theme, or undefined when
 *  the theme draws none or the pack has no role. */
export function packColor(pack: string): string | undefined {
  const role = packRole(pack);
  const color = role && canvasColors().roleAccents[role];
  return color ? css(color) : undefined;
}

/** The pack of a kind: its dotted prefix ("table.filter" -> "table"). */
export const packOf = (kind: string): string => kind.slice(0, kind.indexOf(".") < 0 ? kind.length : kind.indexOf("."));

/** A small coloured dot for a pack, or null when the theme gives it no hue. */
export function packDot(doc: Document, className: string, pack: string): HTMLElement | null {
  const color = packColor(pack);
  if (!color) return null;
  const dot = doc.createElement("i");
  dot.className = className;
  dot.style.background = color;
  dot.setAttribute("aria-hidden", "true");
  return dot;
}

/** The writer glyph as inline SVG: the same arrow into a tray the card draws. */
function writerSvg(doc: Document): SVGElement {
  const svg = doc.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("viewBox", "0 0 12 12");
  svg.setAttribute("aria-hidden", "true");
  const path = doc.createElementNS("http://www.w3.org/2000/svg", "path");
  path.setAttribute("d", "M6 0.5v6.5M3 4.5l3 3 3-3M0.5 8.5v3h11v-3");
  path.setAttribute("fill", "none");
  path.setAttribute("stroke", "currentColor");
  path.setAttribute("stroke-width", "1.4");
  svg.append(path);
  return svg;
}

/** Fills `list` (a `dl`) with the key's rows; call again after a theme change. */
export function renderPackKey(doc: Document, list: HTMLElement): void {
  list.replaceChildren();
  for (const entry of keyEntries()) {
    const dt = doc.createElement("dt");
    dt.className = `bof-key-swatch bof-key-${entry.swatch}`;
    if (entry.swatch === "border") dt.style.borderColor = entry.color;
    else if (entry.swatch === "writer") dt.append(writerSvg(doc));
    const dd = doc.createElement("dd");
    dd.textContent = entry.label;
    list.append(dt, dd);
  }
}
