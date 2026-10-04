import { describe, expect, it } from "vitest";
import { mountHostBanner } from "../src/hostBanner";
import type { HostStatusSource, HostStatusState } from "../src/hostStatus";

/** A host status whose state the test sets by hand. */
function fakeHost(initial: HostStatusState["status"]) {
  let state: HostStatusState = { status: initial, failures: 0 };
  const listeners = new Set<(s: HostStatusState) => void>();
  const host = {
    get: () => state,
    subscribe: (fn: (s: HostStatusState) => void) => { listeners.add(fn); return () => listeners.delete(fn); },
  } as unknown as HostStatusSource;
  const set = (status: HostStatusState["status"]) => { state = { status, failures: 0 }; listeners.forEach((fn) => fn(state)); };
  return { host, set, listeners };
}

describe("mountHostBanner", () => {
  it("is hidden while connected and names the url when not", () => {
    const { host, set } = fakeHost("connected");
    mountHostBanner(document, host, "http://h/api");
    const banner = document.querySelector<HTMLElement>(".bof-app-host-banner")!;
    expect(banner.hidden).toBe(true);
    set("offline");
    expect(banner.hidden).toBe(false);
    expect(banner.textContent).toContain("http://h/api");
    expect(banner.classList.contains("bof-app-host-banner-offline")).toBe(true);
  });

  it("brings its own style once, and the unsubscribe removes the banner", () => {
    const { host, listeners } = fakeHost("reconnecting");
    const stop = mountHostBanner(document, host);
    mountHostBanner(document, host)();
    expect(document.querySelectorAll("#bof-host-banner-styles")).toHaveLength(1);
    stop();
    expect(listeners.size).toBe(0);
  });
});
