import { defineConfig, mergeConfig } from "vitest/config";
import base from "./vitest.config";

// The sample-layout check (TKT-110), kept out of `npm test`: it needs a
// running host and warns rather than gates. `npm run test:layout`.
export default mergeConfig(base, defineConfig({ test: { include: ["test/layout/**/*.check.ts"] } }));
