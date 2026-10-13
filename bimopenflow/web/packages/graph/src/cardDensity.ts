// How much each canvas card shows (TKT-296): every parameter editor and the
// description (full), or the header, the ports, the wired parameter rows, and
// one summary line (compact). One density for the whole page, read by the
// canvas cards (viewModel.cardContent), with a change signal every mounted
// canvas re-sizes its cards on. The descriptionLevel.ts pattern; gratify-free
// and DOM-free.

export const cardDensities = ["full", "compact"] as const;

export type CardDensity = (typeof cardDensities)[number];

export const defaultCardDensity: CardDensity = "full";

/** Longest a compact card's summary line gets before it ends in an ellipsis. */
export const SUMMARY_MAX_CHARS = 44;

export const isCardDensity = (value: string): value is CardDensity =>
  (cardDensities as readonly string[]).includes(value);

/** The other density: the one control toggles between the two. */
export const nextCardDensity = (density: CardDensity): CardDensity => (density === "full" ? "compact" : "full");

/** One line for a compact card: the first of `names` (catalog order) that
 *  `values` sets to a non-blank value, as "name = value" with whitespace
 *  collapsed and cut to SUMMARY_MAX_CHARS. "" when the document sets none. */
export function summaryLine(names: readonly string[], values: Readonly<Record<string, string>>): string {
  const name = names.find((n) => (values[n] ?? "").trim() !== "");
  if (name === undefined) return "";
  const line = `${name} = ${values[name]!.replace(/\s+/g, " ").trim()}`;
  return line.length > SUMMARY_MAX_CHARS ? `${line.slice(0, SUMMARY_MAX_CHARS - 1)}…` : line;
}

let current: CardDensity = defaultCardDensity;
const listeners = new Set<() => void>();

export const currentCardDensity = (): CardDensity => current;

/** Switches the density and notifies every listener once; a no-op when
 *  `density` is already current. */
export function setCardDensity(density: CardDensity): void {
  if (density === current) return;
  current = density;
  for (const listener of [...listeners]) listener();
}

/** Calls `listener` after each density change; returns the unsubscribe. */
export function onCardDensityChange(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
