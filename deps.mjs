#!/usr/bin/env node
// Fills deps/ with the repositories listed in deps.json, so the build reads
// each dependency from deps/<name> and from nowhere else.
//
//   node deps.mjs           link or clone every missing dependency
//   node deps.mjs --check   print each dependency's mode and commit against its pin
//
// For each dependency, the first rule that applies:
//   1. deps/<name> exists: keep it, and compare its commit with the pin.
//   2. This repository's parent folder holds a .deps-root marker: make sure
//      ../<name> exists there (cloning it if not), then link deps/<name> to it.
//   3. Otherwise clone the dependency into deps/<name> at the pinned commit.
// deps/ gets a .deps-root marker, so each dependency's own deps.json resolves
// to siblings in the same folder: one checkout of every repository.
// The dependencies of dependencies are linked into this repository's deps/
// too, so in both modes deps/ holds every repository the build reaches, and
// the build names each one by a single path: deps/<name>.
//
// The same file is copied into every BIM Open repository; keep the copies
// identical. Design: docs/plans/repository-split.md in bim-open-toolkit.
import { execFileSync } from "node:child_process";
import { existsSync, lstatSync, mkdirSync, readFileSync, realpathSync, symlinkSync, writeFileSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const MARKER = ".deps-root";
const check = process.argv.includes("--check");
const root = dirname(fileURLToPath(import.meta.url));
const rootDeps = join(root, "deps");

const git = (cwd, ...args) => execFileSync("git", args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
const commitOf = (dir) => { try { return git(dir, "rev-parse", "HEAD"); } catch { return undefined; } };
const short = (commit) => (commit ? commit.slice(0, 7) : "-------");
const readDeps = (repo) => {
  const file = join(repo, "deps.json");
  return existsSync(file) ? JSON.parse(readFileSync(file, "utf8")) : {};
};
const isDepsRoot = (dir) => existsSync(join(dir, MARKER));
const isLink = (path) => { try { return lstatSync(path).isSymbolicLink(); } catch { return false; } };

const markDepsRoot = (dir) => {
  mkdirSync(dir, { recursive: true });
  const marker = join(dir, MARKER);
  if (!existsSync(marker)) writeFileSync(marker, "Repositories in this folder are dependencies resolved by deps.mjs.\n");
};

const clone = (name, spec, target) => {
  console.log(`clone  ${name} -> ${target}`);
  execFileSync("git", ["clone", "--quiet", spec.url, target], { stdio: "inherit" });
  if (spec.commit) git(target, "checkout", "--quiet", "-B", "main", spec.commit);
};

const link = (name, source, target) => {
  console.log(`link   ${name} -> ${source}`);
  symlinkSync(resolve(source), target, process.platform === "win32" ? "junction" : "dir");
};

// One pin per repository name for the whole run: the first wins, a different one is reported.
const pins = new Map();
const problems = [];
const notePin = (name, spec, from) => {
  const seen = pins.get(name);
  if (!seen) pins.set(name, { commit: spec.commit, from });
  else if (spec.commit && seen.commit && spec.commit !== seen.commit)
    problems.push(`${name}: ${basename(from)} pins ${short(spec.commit)}, ${basename(seen.from)} pins ${short(seen.commit)}; using ${short(seen.commit)}`);
};

const report = (name, target, spec) => {
  const commit = commitOf(target);
  const mode = !existsSync(target) ? "missing" : isLink(target) ? "linked" : "cloned";
  const match = !spec.commit ? "unpinned" : commit === spec.commit ? "matches pin" : `pin is ${short(spec.commit)}`;
  console.log(`${mode.padEnd(7)} ${name.padEnd(22)} ${short(commit)}  ${match}${mode === "linked" ? `  (${realpathSync(target)})` : ""}`);
  if (mode === "missing") problems.push(`${name}: missing at ${target}; run node deps.mjs`);
};

const visited = new Set();
const resolveRepo = (repo) => {
  const real = realpathSync(repo);
  if (visited.has(real)) return;
  visited.add(real);
  const deps = readDeps(repo);
  const names = Object.keys(deps);
  if (names.length === 0) return;
  const parent = dirname(real);
  const depsDir = join(repo, "deps");
  if (!check) markDepsRoot(depsDir);
  // A repository's own pins are noted before any dependency's, so they win.
  for (const name of names) notePin(name, deps[name], repo);
  for (const name of names) {
    const spec = deps[name];
    const target = join(depsDir, name);
    const flat = join(rootDeps, name);
    if (check) {
      report(name, target, pins.get(name));
      if (repo !== root && existsSync(target) && !existsSync(flat)) problems.push(`${name}: missing at ${flat}; run node deps.mjs`);
      if (existsSync(target)) resolveRepo(target);
      continue;
    }
    if (!existsSync(target)) {
      if (isDepsRoot(parent)) {
        const sibling = join(parent, name);
        if (!existsSync(sibling)) clone(name, { ...spec, commit: pins.get(name).commit }, sibling);
        link(name, sibling, target);
      } else {
        clone(name, { ...spec, commit: pins.get(name).commit }, target);
      }
    }
    const commit = commitOf(target);
    const pinned = pins.get(name).commit;
    if (pinned && commit !== pinned) problems.push(`${name}: at ${short(commit)}, pinned ${short(pinned)} (${target})`);
    resolveRepo(target);
    if (!existsSync(flat)) link(name, realpathSync(target), flat);
  }
};

resolveRepo(root);
for (const problem of problems) console.warn(`warning: ${problem}`);
if (check && problems.some((p) => p.includes("missing"))) process.exitCode = 1;
