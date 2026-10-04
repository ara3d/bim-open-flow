import { describe, expect, it } from "vitest";
import { startOnce } from "../src/startOnce";

describe("startOnce", () => {
  it("shares one in-flight attempt between overlapping triggers", async () => {
    let done = false;
    let calls = 0;
    let finish!: () => void;
    const once = startOnce(() => { calls++; return new Promise<void>((r) => { finish = () => { done = true; r(); }; }); }, () => done);
    const first = once();
    const second = once();
    expect(calls).toBe(1);
    finish();
    await Promise.all([first, second]);
    await once();
    expect(calls).toBe(1);
  });

  it("retries after a failed attempt and stops once it succeeds", async () => {
    let done = false;
    let calls = 0;
    const once = startOnce(async () => { calls++; if (calls === 1) throw new Error("host down"); done = true; }, () => done);
    await expect(once()).rejects.toThrow("host down");
    await once();
    await once();
    expect(calls).toBe(2);
  });
});
