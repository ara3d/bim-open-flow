// The human title of an analysis id for the studio's flow picker and
// heading: the sample catalog's title when the id names a sample that has
// one, else the id with its dashes and underscores read as spaces
// ("clash-candidates" -> "Clash candidates"). Pure.

import type { FlowTemplate } from "../templates.js";

export function flowTitle(id: string, templates: readonly FlowTemplate[]): string {
  const template = templates.find((t) => t.id === id);
  if (template && template.title !== template.id) return template.title;
  const words = id.replace(/[-_]+/g, " ").trim();
  return words ? words[0]!.toUpperCase() + words.slice(1) : id;
}
