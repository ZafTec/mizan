#!/usr/bin/env node
/**
 * Static route audit for the frontend.
 *
 * Answers, for every app-router page: is it reachable, does it look like a
 * real screen, and what backend surface does it depend on. Static analysis
 * only - it cannot prove a route renders. Treat FLAGS as leads to verify,
 * not verdicts.
 *
 * Usage: node scripts/route-audit.mjs [--json]
 */
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const APP = "frontend/app";
const SCAN = ["frontend/app", "frontend/components", "frontend/lib"];
const STUB_LOC = 30;

const walk = (dir, out = []) => {
  for (const e of readdirSync(dir)) {
    const p = join(dir, e);
    if (statSync(p).isDirectory()) walk(p, out);
    else out.push(p);
  }
  return out;
};

const routeOf = (file) =>
  "/" +
  relative(APP, file)
    .replace(/\/?page\.tsx$/, "")
    .split("/")
    .filter((s) => s && !/^\(.+\)$/.test(s))
    .join("/");

const allFiles = SCAN.flatMap((d) => walk(d));
const pages = walk(APP).filter((f) => f.endsWith("page.tsx"));

// Every href/router target mentioned anywhere in the frontend source.
const linkTargets = new Set();
const source = new Map();
for (const f of allFiles) {
  if (!/\.(tsx?|mts)$/.test(f)) continue;
  const text = readFileSync(f, "utf8");
  source.set(f, text);
  // handles href="/x", href={"/x"}, href={`/x/${id}`}, router.push(`/x`), redirect("/x")
  for (const m of text.matchAll(
    /(?:href|router\.(?:push|replace)|redirect)\s*[=:(]?\s*\{?\s*["'`](\/[^"'`\s?#]*)/g
  )) {
    // collapse interpolated segments so /x/${id}/edit matches /x/[id]/edit
    const t = m[1].replace(/\$\{[^}]*\}/g, ":param").replace(/\/$/, "");
    linkTargets.add(t || "/");
  }
}

// Dynamic routes match by prefix: /recipes/[id] is linked by /recipes/abc.
const isLinked = (route) => {
  if (linkTargets.has(route)) return true;
  const re = new RegExp(
    "^" + route.replace(/\[[^\]]+\]/g, "[^/]+").replace(/\//g, "\\/") + "$"
  );
  for (const t of linkTargets) if (re.test(t)) return true;
  return false;
};

const NAV_FILE = "frontend/components/Layout/AppShell.tsx";
const navText = source.get(NAV_FILE) ?? "";
const navHrefs = new Set(
  [...navText.matchAll(/href:\s*["'`](\/[^"'`]*)/g)].map((m) => m[1])
);

const rows = pages.map((file) => {
  const route = routeOf(file);
  const text = readFileSync(file, "utf8");
  const loc = text.split("\n").length;
  const dir = file.replace(/page\.tsx$/, "");
  const nested = new Set(
    pages.filter((p) => p !== file && p.startsWith(dir)).map((p) => p.replace(/page\.tsx$/, ""))
  );
  const dirFiles = allFiles.filter(
    (f) =>
      f.startsWith(dir) &&
      /\.tsx?$/.test(f) &&
      ![...nested].some((n) => f.startsWith(n))
  );
  const featureLoc = dirFiles.reduce(
    (n, f) => n + (source.get(f)?.split("\n").length ?? 0),
    0
  );
  const body = dirFiles.map((f) => source.get(f) ?? "").join("\n");

  const endpoints = [
    ...new Set(
      [...body.matchAll(/["'`]\/api\/([A-Za-z][A-Za-z0-9]*)/g)].map((m) => m[1])
    ),
  ].sort();

  const flags = [];
  if (!isLinked(route)) flags.push("ORPHAN");
  if (navHrefs.has(route)) flags.push("in-nav");
  if (loc <= STUB_LOC && featureLoc <= STUB_LOC) flags.push("STUB");
  if (/coming soon|not implemented|\bTODO\b|\bFIXME\b/i.test(body))
    flags.push("TODO");
  if (endpoints.length === 0) flags.push("no-fetch");

  return { route, file, loc, featureLoc, endpoints, flags };
});

rows.sort((a, b) => a.route.localeCompare(b.route));

if (process.argv.includes("--json")) {
  console.log(JSON.stringify(rows, null, 2));
} else {
  const pad = (s, n) => String(s).padEnd(n);
  console.log(pad("ROUTE", 38) + pad("LOC", 7) + pad("FLAGS", 26) + "API");
  console.log("-".repeat(110));
  for (const r of rows) {
    console.log(
      pad(r.route, 38) +
        pad(r.featureLoc, 7) +
        pad(r.flags.join(",") || "-", 26) +
        (r.endpoints.join(",") || "-")
    );
  }
  const orphans = rows.filter((r) => r.flags.includes("ORPHAN"));
  console.log(
    `\n${rows.length} routes · ${navHrefs.size} in nav · ${orphans.length} orphaned · ` +
      `${rows.filter((r) => r.flags.includes("STUB")).length} stubs`
  );
}
