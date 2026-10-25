import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Every "/api/..." path the frontend hands to clientApi, serverApi or request
 * must exist in the generated OpenAPI paths. Calls to routes the backend no
 * longer serves fail silently behind .catch fallbacks, so this test catches
 * them at build time.
 */

const frontendRoot = join(__dirname, "..");
const scanned = ["app", "components", "lib", "data"];
const sourcePattern = /\.(ts|tsx)$/;

function listSources(dir: string): string[] {
  const files: string[] = [];
  for (const entry of readdirSync(dir)) {
    if (entry === "node_modules" || entry.startsWith(".")) continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) files.push(...listSources(full));
    else if (sourcePattern.test(entry) && !/\.test\.tsx?$/.test(entry))
      files.push(full);
  }
  return files;
}

function generatedPaths(): string[] {
  const source = readFileSync(
    join(frontendRoot, "types/api.generated.ts"),
    "utf8",
  );
  const start = source.indexOf("export interface paths {");
  const end = source.indexOf("\nexport ", start + 1);
  const block = source.slice(start, end);
  return [...block.matchAll(/^ {4}"(\/[^"]+)": \{/gm)].map((m) => m[1]);
}

/** `{id}`-style segments in a generated key match any single path segment. */
function toMatcher(generatedPath: string): RegExp {
  const pattern = generatedPath
    .split("/")
    .map((segment) =>
      /^\{[^}]+\}$/.test(segment)
        ? "[^/]+"
        : segment.replace(/[.*+?^$()|[\]\\]/g, "\\$&"),
    )
    .join("/");
  return new RegExp(`^${pattern}$`);
}

interface Call {
  file: string;
  line: number;
  path: string;
}

const genericArgs = String.raw`(?:<(?:[^<>]|<(?:[^<>]|<[^<>]*>)*>)*>)?`;
const callPattern = new RegExp(
  String.raw`\b(?:clientApi|serverApi|request)${genericArgs}\s*\(\s*(["'\x60])(\/api\/(?:[^"'\x60\\]|\\.)*)\1`,
  "g",
);

function normalize(literal: string): string {
  const withoutQuery = literal.split("?")[0];
  return withoutQuery.replace(/\$\{[^}]*\}/g, "{param}");
}

function findCalls(): Call[] {
  const calls: Call[] = [];
  for (const dir of scanned) {
    for (const file of listSources(join(frontendRoot, dir))) {
      const source = readFileSync(file, "utf8");
      for (const match of source.matchAll(callPattern)) {
        const line = source.slice(0, match.index).split("\n").length;
        calls.push({
          file: relative(frontendRoot, file),
          line,
          path: normalize(match[2]),
        });
      }
    }
  }
  return calls;
}

describe("frontend API routes", () => {
  const matchers = generatedPaths().map(toMatcher);
  const calls = findCalls();

  it("finds literal API calls to check", () => {
    expect(matchers.length).toBeGreaterThan(50);
    expect(calls.length).toBeGreaterThan(20);
  });

  it("only calls routes the API serves", () => {
    const missing = calls
      .filter((call) => !matchers.some((matcher) => matcher.test(call.path)))
      .map((call) => `${call.file}:${call.line} ${call.path}`);
    expect(missing).toEqual([]);
  });
});
