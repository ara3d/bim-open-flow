import type { EntityParameter, EntityProperties } from "@bimopenflow/contracts";

/** Parameters of one property set, in the order the host sent them. */
export interface EntityPropertyGroup {
  name: string;
  parameters: EntityParameter[];
}

/**
 * Consecutive runs of parameters sharing a group. The host already sorts by
 * group then name, so one pass is enough and the display order is the host's.
 */
export const groupParameters = (
  parameters: readonly EntityParameter[],
): EntityPropertyGroup[] => {
  const groups: EntityPropertyGroup[] = [];
  for (const parameter of parameters) {
    const last = groups[groups.length - 1];
    if (last?.name === parameter.group) last.parameters.push(parameter);
    else groups.push({ name: parameter.group, parameters: [parameter] });
  }
  return groups;
};

/** "12.5 kg" for a parameter with units, "12.5" without. */
export const parameterText = (parameter: EntityParameter): string =>
  parameter.units ? `${parameter.value} ${parameter.units}` : parameter.value;

const el = (doc: Document, tag: string, className: string, text?: string): HTMLElement => {
  const node = doc.createElement(tag);
  node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
};

/**
 * Replaces the contents of root with one entity's identity and property sets,
 * using the same dl pattern as the inspector pane.
 */
export const renderEntityProperties = (root: HTMLElement, props: EntityProperties): void => {
  const doc = root.ownerDocument;
  const parts: HTMLElement[] = [
    el(doc, "div", "bof-panes-title", props.name ?? `Entity ${props.localId}`),
  ];
  const identity: Array<[string, string]> = [["id", String(props.localId)]];
  if (props.category) identity.push(["category", props.category]);
  if (props.globalId) identity.push(["globalId", props.globalId]);
  parts.push(dl(doc, identity));
  for (const group of groupParameters(props.parameters)) {
    parts.push(el(doc, "div", "bof-panes-section", group.name));
    parts.push(dl(doc, group.parameters.map((p) => [p.name, parameterText(p)])));
  }
  root.replaceChildren(...parts);
  root.hidden = false;
};

/** Replaces the contents of root with a single line (a failed fetch, usually). */
export const renderEntityMessage = (root: HTMLElement, message: string): void => {
  root.replaceChildren(el(root.ownerDocument, "div", "bof-panes-error", message));
  root.hidden = false;
};

const dl = (doc: Document, entries: Array<[string, string]>): HTMLElement => {
  const list = el(doc, "dl", "bof-panes-dl");
  for (const [term, detail] of entries) {
    list.appendChild(el(doc, "dt", "bof-panes-term", term));
    list.appendChild(el(doc, "dd", "bof-panes-value", detail));
  }
  return list;
};
