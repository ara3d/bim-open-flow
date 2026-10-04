// The node card style seam (TKT-98): which of four card styles the canvas
// draws, and a change signal the editor refreshes on. The styles differ only
// in how a card shows its title, id, description, and status; nodeRender.ts
// owns what each name means. Gratify-free and DOM-free.

export const nodeStyleNames = ["classic", "banner", "chip", "bar"] as const;

export type NodeStyleName = (typeof nodeStyleNames)[number];

export const defaultNodeStyle: NodeStyleName = "classic";

export const isNodeStyleName = (value: string): value is NodeStyleName =>
  (nodeStyleNames as readonly string[]).includes(value);

let current: NodeStyleName = defaultNodeStyle;
const listeners = new Set<() => void>();

export const currentNodeStyle = (): NodeStyleName => current;

/** Switches the style and notifies every listener once; a no-op when `name`
 *  is already current. */
export function setNodeStyle(name: NodeStyleName): void {
  if (name === current) return;
  current = name;
  for (const listener of [...listeners]) listener();
}

/** Calls `listener` after each style change; returns the unsubscribe. */
export function onNodeStyleChange(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
