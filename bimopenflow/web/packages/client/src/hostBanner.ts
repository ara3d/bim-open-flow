// The banner every page shows while the host is not connected. Its style is
// injected here, so a page that does not load the editor's stylesheet (the
// notebook) still gets it; the colours read the editor's tokens when present.

import { hostStatusMessage, type HostStatus, type HostStatusSource } from "./hostStatus";

const STYLE_ID = "bof-host-banner-styles";

const bannerCss = `
.bof-app-host-banner {
  position: fixed; left: 0; right: 0; bottom: 0; z-index: 90;
  padding: 10px 16px; text-align: center;
  font: 600 14px/1.4 var(--bof-app-font, "Public Sans", "Segoe UI", system-ui, sans-serif);
  background: var(--bof-app-amber, #d99a2b); color: #1a1a18; box-shadow: 0 -2px 10px rgba(0,0,0,.2);
}
.bof-app-host-banner.bof-app-host-banner-offline { background: var(--bof-app-red, #c0392b); color: #fff; }
.bof-app-host-banner[hidden] { display: none; }
`;

function ensureBannerStyles(doc: Document): void {
  if (doc.getElementById(STYLE_ID)) return;
  const style = doc.createElement("style");
  style.id = STYLE_ID;
  style.textContent = bannerCss;
  doc.head.appendChild(style);
}

/**
 * A fixed strip along the bottom of the page that names the host API url and
 * how to recover whenever the host is not connected; hidden while it is.
 * Returns the unsubscribe.
 */
export function mountHostBanner(
  doc: Document,
  host: HostStatusSource,
  apiUrl = new URL("/api", doc.baseURI).href,
): () => void {
  ensureBannerStyles(doc);
  const banner = doc.createElement("div");
  banner.className = "bof-app-host-banner";
  banner.setAttribute("role", "alert");
  const render = (status: HostStatus) => {
    banner.textContent = hostStatusMessage(status, apiUrl);
    banner.hidden = status === "connected";
    banner.classList.toggle("bof-app-host-banner-offline", status === "offline");
  };
  render(host.get().status);
  doc.body.appendChild(banner);
  const unsubscribe = host.subscribe((state) => render(state.status));
  return () => {
    unsubscribe();
    banner.remove();
  };
}
