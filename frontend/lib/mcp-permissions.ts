import type { ScopeGroup } from "@/types/mcp";

/**
 * What a person picks for one permission group. Write includes read, so there
 * are only three honest answers. A group with no write scope (the assistant,
 * administration) is either on, which this calls "read", or off.
 */
export type AccessLevel = "none" | "read" | "write";
export type AccessLevels = Record<string, AccessLevel>;
export type HouseholdMode = "none" | "selected" | "all";

const RANK: Record<AccessLevel, number> = { none: 0, read: 1, write: 2 };

export function levelRank(level: AccessLevel): number {
  return RANK[level];
}

export function levelsFromScopes(groups: ScopeGroup[], scopes: readonly string[]): AccessLevels {
  const held = new Set(scopes);
  const levels: AccessLevels = {};

  for (const group of groups) {
    if (group.writeScope && held.has(group.writeScope)) levels[group.group] = "write";
    else if (held.has(group.readScope)) levels[group.group] = "read";
    else levels[group.group] = "none";
  }

  return levels;
}

/** The scopes a set of levels stands for. Write also lists read, as the server stores it. */
export function scopesFromLevels(groups: ScopeGroup[], levels: AccessLevels): string[] {
  const scopes: string[] = [];

  for (const group of groups) {
    const level = levels[group.group] ?? "none";
    if (level === "none") continue;
    scopes.push(group.readScope);
    if (level === "write" && group.writeScope) scopes.push(group.writeScope);
  }

  return scopes;
}

/** The highest level each group may be set to, given what is already held. */
export function ceilingFromScopes(groups: ScopeGroup[], scopes: readonly string[]): AccessLevels {
  return levelsFromScopes(groups, scopes);
}

export function levelLabel(group: ScopeGroup, level: AccessLevel): string {
  if (level === "none") return "No access";
  if (!group.hasWrite) return "Allowed";
  return level === "read" ? "Read" : "Read and change";
}

/** "Food and nutrition: read and change. Training: read." */
export function summarizeAccess(groups: ScopeGroup[], scopes: readonly string[]): string[] {
  const levels = levelsFromScopes(groups, scopes);

  return groups
    .filter((group) => levels[group.group] !== "none")
    .map((group) => {
      const level = levels[group.group];
      if (!group.hasWrite) return group.title;
      return `${group.title}: ${level === "write" ? "read and change" : "read"}`;
    });
}

export function householdSummary(mode: string, names: readonly string[]): string {
  if (mode === "all") return "All of your households";
  if (mode === "selected") return names.length > 0 ? names.join(", ") : "Selected households";
  return "Personal data only";
}

/**
 * Default levels for the consent screen. If the app asked for specific scopes,
 * those are on and everything else is off, because a person cannot grant more
 * than was asked. If it asked for nothing, reading is on and the rest is off.
 */
export function defaultLevels(groups: ScopeGroup[], requested: readonly string[]): AccessLevels {
  if (requested.length === 0) {
    const levels: AccessLevels = {};
    for (const group of groups) {
      levels[group.group] = group.group === "ai" || group.group === "admin" ? "none" : "read";
    }
    return levels;
  }

  return levelsFromScopes(groups, requested);
}
