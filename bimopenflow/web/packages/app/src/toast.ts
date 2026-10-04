let host: HTMLElement | null = null;

/** Transient bottom-right notification. */
export function showToast(message: string, kind: "info" | "error" = "info"): void {
  if (!host || !host.isConnected) {
    host = document.createElement("div");
    host.className = "bof-app-toasts";
    document.body.appendChild(host);
  }
  const el = document.createElement("div");
  el.className = "bof-app-toast" + (kind === "error" ? " bof-app-toast-error" : "");
  el.textContent = message;
  host.appendChild(el);
  setTimeout(() => el.remove(), 5000);
}
