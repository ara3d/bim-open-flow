// A plain-DOM multi-line editor for a long parameter value (Json, Expression,
// long Text). It knows nothing about nodes, gratify, or canvas intents: the
// caller (canvasLongSlot.ts, C5) places its wrapper as an island and turns
// onCommit into a setParam intent. Keeping the DOM and the intent flow apart
// is what lets this file be tested without a canvas or a store.
//
// Commit and discard happen at most once per opening (see "One undo step per
// edit" in docs/plans/remove-properties-panel.md): every commit becomes one
// setParam, and typing itself never dispatches anything.

export interface LongValueEditorOpen {
  /** Accessible name and heading, e.g. "Rules (JSON)". */
  readonly label: string;
  /** Value loaded when opening, and the target of a discard. */
  readonly value: string;
  /** At most once per opening, only when the text differs from value. */
  readonly onCommit: (value: string) => void;
  /** Exactly once per user-initiated close (commit or discard), after onCommit. */
  readonly onClose: () => void;
}

export interface LongValueEditor {
  /** Stable wrapper (textarea + Apply + Cancel), z-index 1; the caller places it. */
  readonly el: HTMLElement;
  readonly textarea: HTMLTextAreaElement;
  /** Loads value, focuses the textarea. */
  open(request: LongValueEditorOpen): void;
  isOpen(): boolean;
  /** Closes silently (no callbacks): the canvas closed the editor itself. */
  close(): void;
  /** close() plus removal of listeners and the element. */
  dispose(): void;
}

export function createLongValueEditor(doc: Document): LongValueEditor {
  let current: LongValueEditorOpen | null = null;

  const el = doc.createElement("div");
  el.style.cssText =
    "box-sizing:border-box;display:flex;flex-direction:column;gap:6px;" +
    "width:100%;height:100%;padding:8px;border-radius:6px;z-index:1;" +
    "border:1px solid #5b6472;background:#20242c;color:#e6e8eb;" +
    "font:14px system-ui,'Segoe UI',sans-serif;";

  const label = doc.createElement("div");
  label.style.cssText = "font-weight:600;opacity:0.85;flex:none;";
  el.appendChild(label);

  const textarea = doc.createElement("textarea");
  textarea.style.cssText =
    "box-sizing:border-box;flex:1 1 auto;width:100%;resize:none;" +
    "border-radius:4px;padding:6px 8px;font:13px ui-monospace,'Cascadia Code',monospace;" +
    "border:1px solid #5b6472;background:#14171c;color:#e6e8eb;outline:none;";
  textarea.setAttribute("aria-label", "Long value editor");
  el.appendChild(textarea);

  const buttons = doc.createElement("div");
  buttons.style.cssText = "display:flex;justify-content:flex-end;gap:6px;flex:none;";
  el.appendChild(buttons);

  const cancelButton = doc.createElement("button");
  cancelButton.type = "button";
  cancelButton.textContent = "Cancel";
  cancelButton.style.cssText =
    "padding:4px 10px;border-radius:4px;border:1px solid #5b6472;background:#2b3038;color:#e6e8eb;cursor:pointer;";
  buttons.appendChild(cancelButton);

  const applyButton = doc.createElement("button");
  applyButton.type = "button";
  applyButton.textContent = "Apply";
  applyButton.style.cssText =
    "padding:4px 10px;border-radius:4px;border:1px solid #5b6472;background:#3a6fd8;color:#fff;cursor:pointer;";
  buttons.appendChild(applyButton);

  // finish(commit) is the single exit path for a user-initiated close: it
  // commits at most once (only when the text changed), then closes exactly
  // once. `current` is cleared first so a callback that re-enters (for
  // example a focusout fired while onClose tears the row down) cannot
  // trigger a second commit or close for the same opening.
  function finish(commit: boolean): void {
    const request = current;
    if (!request) return;
    current = null;
    if (commit && textarea.value !== request.value) {
      request.onCommit(textarea.value);
    }
    request.onClose();
  }

  function onKeyDown(e: KeyboardEvent): void {
    if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      finish(true);
      return;
    }
    if (e.key === "Escape") {
      e.stopPropagation();
      finish(false);
    }
    // Plain Enter is left alone: the textarea inserts a newline by default.
  }

  function onFocusOut(e: FocusEvent): void {
    const next = e.relatedTarget as Node | null;
    if (next && el.contains(next)) return;
    finish(true);
  }

  function onApply(): void {
    finish(true);
  }

  function onCancel(): void {
    finish(false);
  }

  textarea.addEventListener("keydown", onKeyDown);
  el.addEventListener("focusout", onFocusOut);
  applyButton.addEventListener("click", onApply);
  cancelButton.addEventListener("click", onCancel);

  return {
    el,
    textarea,

    open(request: LongValueEditorOpen): void {
      current = request;
      label.textContent = request.label;
      textarea.value = request.value;
      // Gratify mounts the island (and connects `el` to the page) after this
      // function returns (runtime.ts:552-571), so focusing now would focus a
      // detached element and do nothing: Ctrl+Enter, Escape, and click-outside
      // would need a manual click first. Focus at once when already connected
      // (the caller re-opens an editor that is already on the page), otherwise
      // wait a microtask for the mount, and only if this is still the current
      // opening.
      if (el.isConnected) {
        textarea.focus();
      } else {
        queueMicrotask(() => {
          if (current === request) textarea.focus();
        });
      }
    },

    isOpen(): boolean {
      return current !== null;
    },

    close(): void {
      current = null;
    },

    dispose(): void {
      current = null;
      textarea.removeEventListener("keydown", onKeyDown);
      el.removeEventListener("focusout", onFocusOut);
      applyButton.removeEventListener("click", onApply);
      cancelButton.removeEventListener("click", onCancel);
      el.remove();
    },
  };
}
