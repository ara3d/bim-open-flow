import type { Pane, PaneContext, PaneEvent, PaneInput } from "./pane";
import { ensurePaneStyles } from "./styles";

/** What a pane implementation provides; the shell supplies the lifecycle. */
export interface PaneBody {
  update(input: PaneInput): void;
  destroy?(): void;
}

/**
 * Shared lifecycle shell for all panes: creates a .bof-panes-root element,
 * injects styles, guards mount/update ordering, and fans events out to every
 * onEvent handler. `create` runs once at mount.
 */
export const definePane = (
  create: (
    root: HTMLElement,
    ctx: PaneContext,
    emit: (e: PaneEvent) => void,
  ) => PaneBody,
): Pane => {
  let body: PaneBody | null = null;
  let root: HTMLElement | null = null;
  const handlers: Array<(e: PaneEvent) => void> = [];
  const emit = (e: PaneEvent): void => {
    for (const h of [...handlers]) h(e);
  };
  return {
    mount(el, ctx) {
      if (root) throw new Error("bof-panes: pane is already mounted");
      const doc = el.ownerDocument;
      ensurePaneStyles(doc);
      root = doc.createElement("div");
      root.className = "bof-panes-root";
      el.appendChild(root);
      body = create(root, ctx, emit);
    },
    update(input) {
      if (!body) throw new Error("bof-panes: pane is not mounted");
      body.update(input);
    },
    onEvent(handler) {
      handlers.push(handler);
    },
    destroy() {
      body?.destroy?.();
      root?.remove();
      root = null;
      body = null;
    },
  };
};
