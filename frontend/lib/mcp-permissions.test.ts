import { describe, expect, it } from "vitest";
import {
  defaultLevels,
  householdSummary,
  levelsFromScopes,
  scopesFromLevels,
  summarizeAccess,
} from "@/lib/mcp-permissions";
import type { ScopeGroup } from "@/types/mcp";

const groups: ScopeGroup[] = [
  { group: "nutrition", title: "Food and nutrition", description: "", hasWrite: true, readScope: "nutrition:read", writeScope: "nutrition:write" },
  { group: "training", title: "Training", description: "", hasWrite: true, readScope: "training:read", writeScope: "training:write" },
  { group: "ai", title: "Mizan assistant", description: "", hasWrite: false, readScope: "ai:use", writeScope: null },
];

describe("levelsFromScopes", () => {
  it("reads write as the highest level and an unheld group as none", () => {
    expect(levelsFromScopes(groups, ["nutrition:read", "nutrition:write", "training:read"])).toEqual({
      nutrition: "write",
      training: "read",
      ai: "none",
    });
  });

  it("treats a write scope alone as write", () => {
    expect(levelsFromScopes(groups, ["nutrition:write"]).nutrition).toBe("write");
  });

  it("reads a group with no write scope as allowed", () => {
    expect(levelsFromScopes(groups, ["ai:use"]).ai).toBe("read");
  });
});

describe("scopesFromLevels", () => {
  it("lists read alongside write, the way the server stores it", () => {
    expect(scopesFromLevels(groups, { nutrition: "write", training: "read", ai: "none" })).toEqual([
      "nutrition:read",
      "nutrition:write",
      "training:read",
    ]);
  });

  it("round-trips", () => {
    const scopes = ["nutrition:read", "nutrition:write", "ai:use"];
    expect(scopesFromLevels(groups, levelsFromScopes(groups, scopes))).toEqual(scopes);
  });

  it("never writes a write scope for a group that has none", () => {
    expect(scopesFromLevels(groups, { ai: "write" })).toEqual(["ai:use"]);
  });
});

describe("defaultLevels", () => {
  it("turns on only what the app asked for", () => {
    expect(defaultLevels(groups, ["nutrition:read"])).toEqual({ nutrition: "read", training: "none", ai: "none" });
  });

  it("defaults to reading everything but the assistant when the app asked for nothing", () => {
    expect(defaultLevels(groups, [])).toEqual({ nutrition: "read", training: "read", ai: "none" });
  });
});

describe("summaries", () => {
  it("names each group and its level", () => {
    expect(summarizeAccess(groups, ["nutrition:read", "nutrition:write", "training:read", "ai:use"])).toEqual([
      "Food and nutrition: read and change",
      "Training: read",
      "Mizan assistant",
    ]);
  });

  it("says what households an app can see", () => {
    expect(householdSummary("none", [])).toBe("Personal data only");
    expect(householdSummary("all", [])).toBe("All of your households");
    expect(householdSummary("selected", ["Home", "Gym"])).toBe("Home, Gym");
  });
});
