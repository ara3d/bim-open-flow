import { describe, expect, it, vi } from "vitest";
import { createLongValueEditor } from "../src/longValueEditor.js";

function makeEditor() {
  const editor = createLongValueEditor(document);
  document.body.appendChild(editor.el);
  return editor;
}

describe("createLongValueEditor", () => {
  it("commits once and closes once on Ctrl+Enter, then reports closed", () => {
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Rules (JSON)", value: '{"a":1}', onCommit, onClose });

    editor.textarea.value = '{"a":2}';
    editor.textarea.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Enter", ctrlKey: true, bubbles: true, cancelable: true }),
    );

    expect(onCommit).toHaveBeenCalledTimes(1);
    expect(onCommit).toHaveBeenCalledWith('{"a":2}');
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onCommit.mock.invocationCallOrder[0]).toBeLessThan(onClose.mock.invocationCallOrder[0]);
    expect(editor.isOpen()).toBe(false);

    editor.dispose();
  });

  it("also commits on Cmd+Enter (metaKey)", () => {
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });

    editor.textarea.value = "area > 20";
    editor.textarea.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Enter", metaKey: true, bubbles: true, cancelable: true }),
    );

    expect(onCommit).toHaveBeenCalledWith("area > 20");
    expect(onClose).toHaveBeenCalledTimes(1);

    editor.dispose();
  });

  it("Apply with unchanged text calls only onClose", () => {
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });

    const apply = editor.el.querySelector("button:last-of-type") as HTMLButtonElement;
    apply.click();

    expect(onCommit).not.toHaveBeenCalled();
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(editor.isOpen()).toBe(false);

    editor.dispose();
  });

  it("Escape discards, stops propagation, and calls only onClose", () => {
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });

    editor.textarea.value = "area > 999";
    const parentHandler = vi.fn();
    editor.el.parentElement!.addEventListener("keydown", parentHandler);

    const escape = new KeyboardEvent("keydown", { key: "Escape", bubbles: true, cancelable: true });
    editor.textarea.dispatchEvent(escape);

    expect(onCommit).not.toHaveBeenCalled();
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(editor.isOpen()).toBe(false);
    expect(parentHandler).not.toHaveBeenCalled();

    editor.el.parentElement!.removeEventListener("keydown", parentHandler);
    editor.dispose();
  });

  it("mousedown on the label or padding keeps focus, so it never commits", () => {
    // Regression for defect 2: a blur with no relatedTarget (clicking the
    // label or the wrapper's padding) used to look "outside" to onFocusOut
    // and commit the draft.
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });
    editor.textarea.value = "area > 999";

    const label = editor.el.firstElementChild as HTMLElement;
    const mousedown = new MouseEvent("mousedown", { bubbles: true, cancelable: true });
    const prevented = !label.dispatchEvent(mousedown);

    expect(prevented).toBe(true);
    expect(onCommit).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
    expect(editor.isOpen()).toBe(true);

    editor.dispose();
  });

  it("mousedown on Cancel does not prevent its click from discarding", () => {
    // On Safari and Firefox on macOS, clicking a button does not focus it,
    // so mousedown on Cancel must not swallow the click that follows.
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });
    editor.textarea.value = "area > 999";

    const cancel = editor.el.querySelector("button:first-of-type") as HTMLButtonElement;
    cancel.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
    cancel.click();

    expect(onCommit).not.toHaveBeenCalled();
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(editor.isOpen()).toBe(false);

    editor.dispose();
  });

  it("mousedown on the textarea itself does not prevent default", () => {
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });

    const mousedown = new MouseEvent("mousedown", { bubbles: true, cancelable: true });
    const prevented = !editor.textarea.dispatchEvent(mousedown);

    expect(prevented).toBe(false);

    editor.dispose();
  });

  it("Cancel button discards", () => {
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });

    editor.textarea.value = "area > 999";
    const cancel = editor.el.querySelector("button:first-of-type") as HTMLButtonElement;
    cancel.click();

    expect(onCommit).not.toHaveBeenCalled();
    expect(onClose).toHaveBeenCalledTimes(1);

    editor.dispose();
  });

  it("plain Enter inserts a newline instead of committing", () => {
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });

    editor.textarea.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Enter", bubbles: true, cancelable: true }),
    );

    expect(onCommit).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
    expect(editor.isOpen()).toBe(true);

    editor.dispose();
  });

  it("focuses the textarea after mount, even when opened while detached", async () => {
    // Regression for defect 1: gratify connects the island's element to the
    // page only after this function returns, so a synchronous focus() would
    // focus a detached node and silently do nothing.
    const editor = createLongValueEditor(document);
    const onCommit = vi.fn();
    const onClose = vi.fn();

    expect(editor.el.isConnected).toBe(false);
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });
    expect(document.activeElement).not.toBe(editor.textarea);

    document.body.appendChild(editor.el);
    await Promise.resolve();

    expect(document.activeElement).toBe(editor.textarea);

    editor.dispose();
  });

  it("focuses the textarea at once when the editor is already mounted", () => {
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();

    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });

    expect(document.activeElement).toBe(editor.textarea);

    editor.dispose();
  });

  it("a focusout while the document itself has lost focus (window switch) does not commit", () => {
    // Regression for design note 2: Alt+Tab or opening DevTools fires a
    // focusout with no relatedTarget, the same shape onFocusOut otherwise
    // treats as "outside".
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });
    editor.textarea.value = "area > 999";

    const hasFocus = vi.spyOn(document, "hasFocus").mockReturnValue(false);
    editor.textarea.dispatchEvent(new FocusEvent("focusout", { relatedTarget: null, bubbles: true }));

    expect(onCommit).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
    expect(editor.isOpen()).toBe(true);

    hasFocus.mockRestore();
    editor.dispose();
  });

  it("focusout to an element outside the wrapper commits", () => {
    const editor = makeEditor();
    const outside = document.createElement("input");
    document.body.appendChild(outside);
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });

    editor.textarea.value = "area > 20";
    editor.textarea.dispatchEvent(
      new FocusEvent("focusout", { relatedTarget: outside, bubbles: true }),
    );

    expect(onCommit).toHaveBeenCalledWith("area > 20");
    expect(onClose).toHaveBeenCalledTimes(1);

    outside.remove();
    editor.dispose();
  });

  it("focusout to the Apply button (inside the wrapper) does not commit or close", () => {
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });

    const apply = editor.el.querySelector("button:last-of-type") as HTMLButtonElement;
    editor.textarea.value = "area > 20";
    editor.textarea.dispatchEvent(
      new FocusEvent("focusout", { relatedTarget: apply, bubbles: true }),
    );

    expect(onCommit).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
    expect(editor.isOpen()).toBe(true);

    editor.dispose();
  });

  it("close() is silent: no callbacks fire", () => {
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });

    editor.textarea.value = "area > 20";
    editor.close();

    expect(onCommit).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
    expect(editor.isOpen()).toBe(false);

    editor.dispose();
  });

  it("a second open after a commit starts a fresh cycle", () => {
    const editor = makeEditor();
    const firstCommit = vi.fn();
    const firstClose = vi.fn();
    editor.open({ label: "Expr", value: "one", onCommit: firstCommit, onClose: firstClose });
    editor.textarea.value = "two";
    editor.textarea.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Enter", ctrlKey: true, bubbles: true, cancelable: true }),
    );
    expect(firstCommit).toHaveBeenCalledTimes(1);
    expect(firstClose).toHaveBeenCalledTimes(1);

    const secondCommit = vi.fn();
    const secondClose = vi.fn();
    editor.open({ label: "Expr", value: "two", onCommit: secondCommit, onClose: secondClose });
    expect(editor.textarea.value).toBe("two");

    // Unchanged text against the freshly opened value commits nothing.
    const apply = editor.el.querySelector("button:last-of-type") as HTMLButtonElement;
    apply.click();
    expect(secondCommit).not.toHaveBeenCalled();
    expect(secondClose).toHaveBeenCalledTimes(1);
    // The first opening's callbacks never fire again.
    expect(firstCommit).toHaveBeenCalledTimes(1);
    expect(firstClose).toHaveBeenCalledTimes(1);

    editor.dispose();
  });

  it("dispose removes listeners and the element", () => {
    const editor = makeEditor();
    const onCommit = vi.fn();
    const onClose = vi.fn();
    editor.open({ label: "Expr", value: "area > 10", onCommit, onClose });

    editor.dispose();

    expect(editor.el.isConnected).toBe(false);
    expect(editor.isOpen()).toBe(false);

    // Events after dispose do nothing further (listeners were removed).
    editor.textarea.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Enter", ctrlKey: true, bubbles: true, cancelable: true }),
    );
    expect(onCommit).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });
});
