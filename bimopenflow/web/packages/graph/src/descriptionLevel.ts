// How much of a node's catalog description the editor shows (TKT-278): none
// (hidden), its first two sentences (brief), or all of it (full). One level
// for the whole page, read by the canvas cards (viewModel.cardContent) and the
// app's step list, with a change signal the app refreshes both on. The
// canvasTheme.ts pattern; gratify-free and DOM-free.

export const descriptionLevels = ["hidden", "brief", "full"] as const;

export type DescriptionLevel = (typeof descriptionLevels)[number];

export const defaultDescriptionLevel: DescriptionLevel = "brief";

/** Sentences the brief level shows. */
export const BRIEF_SENTENCES = 2;

export const isDescriptionLevel = (value: string): value is DescriptionLevel =>
  (descriptionLevels as readonly string[]).includes(value);

/** The level after `level` in the one control's cycle: hidden, brief, full, hidden. */
export const nextDescriptionLevel = (level: DescriptionLevel): DescriptionLevel =>
  descriptionLevels[(descriptionLevels.indexOf(level) + 1) % descriptionLevels.length]!;

/** The first `count` sentences of a catalog description, joined by spaces.
 *  A sentence ends at ".", "!", or "?" before a space, or at a line end. */
export const leadSentences = (text: string, count: number): string =>
  text.split("\n")
    .flatMap((line) => line.trim().split(/(?<=[.!?])\s+/))
    .filter((s) => s !== "")
    .slice(0, count)
    .join(" ");

/** The part of `text` shown at `level`: "", the brief sentences, or all of it, trimmed. */
export function descriptionText(text: string, level: DescriptionLevel): string {
  if (level === "hidden") return "";
  return level === "brief" ? leadSentences(text, BRIEF_SENTENCES) : text.trim();
}

let current: DescriptionLevel = defaultDescriptionLevel;
const listeners = new Set<() => void>();

export const currentDescriptionLevel = (): DescriptionLevel => current;

/** Switches the level and notifies every listener once; a no-op when `level`
 *  is already current. */
export function setDescriptionLevel(level: DescriptionLevel): void {
  if (level === current) return;
  current = level;
  for (const listener of [...listeners]) listener();
}

/** Calls `listener` after each level change; returns the unsubscribe. */
export function onDescriptionLevelChange(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
