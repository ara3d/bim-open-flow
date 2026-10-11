// Files dropped on the canvas (TKT-138). A browser hides a dropped file's
// folder and gives only its name; a desktop shell (Electron, the Claude Code
// pane) adds the full `path`. The listener passes whichever it gets.

export interface FileDropDeps {
  /** One dropped file: its path (or bare name), and where it landed in client pixels. */
  onFile(path: string, clientX: number, clientY: number): void;
}

const hasFiles = (event: DragEvent): boolean => Array.from(event.dataTransfer?.types ?? []).includes("Files");

/** Installs dragover and drop listeners on `canvas`; returns dispose. */
export function installFileDrop(canvas: HTMLCanvasElement, deps: FileDropDeps): () => void {
  const dragOver = (event: DragEvent) => {
    if (!hasFiles(event)) return;
    event.preventDefault(); // without this the browser refuses the drop
    if (event.dataTransfer) event.dataTransfer.dropEffect = "copy";
  };
  const drop = (event: DragEvent) => {
    if (!hasFiles(event)) return;
    event.preventDefault();
    for (const file of Array.from(event.dataTransfer?.files ?? []))
      deps.onFile((file as File & { path?: string }).path || file.name, event.clientX, event.clientY);
  };
  canvas.addEventListener("dragover", dragOver);
  canvas.addEventListener("drop", drop);
  return () => {
    canvas.removeEventListener("dragover", dragOver);
    canvas.removeEventListener("drop", drop);
  };
}
