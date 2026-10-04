import type { VizComponent } from "./types";
import { ensureStyles } from "./styles";

/**
 * Shared mount/update/destroy scaffolding. `create` receives the root element
 * once and returns the render function; per-instance state (e.g. sort order)
 * lives in the closure it creates.
 */
export const defineComponent = <TData, TOptions>(
  create: (root: HTMLElement, options: TOptions | undefined) => (data: TData) => void,
): VizComponent<TData, TOptions> => ({
  mount(container, data, options) {
    const doc = container.ownerDocument;
    ensureStyles(doc);
    const root = doc.createElement("div");
    root.className = "bof-viz-root";
    container.appendChild(root);
    const render = create(root, options);
    render(data);
    return {
      update: (next) => render(next),
      destroy: () => root.remove(),
    };
  },
});
