"use client";

import { useId } from "react";
import { levelLabel, levelRank, type AccessLevel, type AccessLevels, type HouseholdMode } from "@/lib/mcp-permissions";
import type { ScopeGroup } from "@/types/mcp";

type Household = { id: string; name: string };

/**
 * One row per permission group. A group with a write scope offers no access,
 * read, or read and change. Without one it is simply on or off. `ceiling`
 * stops a row going above what is already held, which is how the edit dialog
 * shows that access can only be taken away there.
 */
export function PermissionPicker({
  groups,
  levels,
  onChange,
  ceiling,
}: {
  groups: ScopeGroup[];
  levels: AccessLevels;
  onChange: (next: AccessLevels) => void;
  ceiling?: AccessLevels;
}) {
  return (
    <div className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
      {groups.map((group) => {
        const max = ceiling?.[group.group] ?? (group.hasWrite ? "write" : "read");
        const options: AccessLevel[] = group.hasWrite ? ["none", "read", "write"] : ["none", "read"];
        const reachable = options.filter((option) => levelRank(option) <= levelRank(max));
        const disabled = reachable.length < 2;

        return (
          <PermissionRow
            key={group.group}
            group={group}
            value={levels[group.group] ?? "none"}
            options={options}
            reachable={reachable}
            disabled={disabled}
            onPick={(level) => onChange({ ...levels, [group.group]: level })}
          />
        );
      })}
    </div>
  );
}

function PermissionRow({
  group,
  value,
  options,
  reachable,
  disabled,
  onPick,
}: {
  group: ScopeGroup;
  value: AccessLevel;
  options: AccessLevel[];
  reachable: AccessLevel[];
  disabled: boolean;
  onPick: (level: AccessLevel) => void;
}) {
  const name = useId();

  return (
    <fieldset className="py-3 sm:flex sm:items-start sm:justify-between sm:gap-6" disabled={disabled}>
      <legend className="sr-only">{group.title}</legend>
      <div className="min-w-0 sm:flex-1">
        <p className="text-sm font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">{group.title}</p>
        <p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">{group.description}</p>
      </div>
      <div className="mt-2 flex flex-wrap gap-1 sm:mt-0" role="radiogroup" aria-label={group.title}>
        {options.map((option) => {
          const allowed = reachable.includes(option);
          const selected = value === option;
          return (
            <label
              key={option}
              className={[
                "cursor-pointer rounded-[3px] border px-3 py-2 text-sm has-[:focus-visible]:outline has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-brand-600",
                selected
                  ? "border-charcoal-blue-900 bg-charcoal-blue-900 text-white dark:border-charcoal-blue-50 dark:bg-charcoal-blue-50 dark:text-charcoal-blue-900"
                  : "border-charcoal-blue-200 text-charcoal-blue-700 dark:border-white/15 dark:text-charcoal-blue-200",
                allowed ? "" : "cursor-not-allowed opacity-40",
              ].join(" ")}
            >
              <input
                type="radio"
                className="sr-only"
                name={name}
                value={option}
                checked={selected}
                disabled={!allowed}
                onChange={() => onPick(option)}
              />
              {levelLabel(group, option)}
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}

/** Which households the app may see: none, every one, or a chosen few. */
export function HouseholdPicker({
  households,
  mode,
  selected,
  onChange,
  ceilingMode = "all",
  ceilingIds,
}: {
  households: Household[];
  mode: HouseholdMode;
  selected: string[];
  onChange: (mode: HouseholdMode, selected: string[]) => void;
  ceilingMode?: HouseholdMode;
  ceilingIds?: string[];
}) {
  const name = useId();
  const rank = { none: 0, selected: 1, all: 2 } as const;
  const offered = ceilingIds ? households.filter((h) => ceilingIds.includes(h.id)) : households;

  const choices: { value: HouseholdMode; label: string; hint: string }[] = [
    { value: "none", label: "Personal data only", hint: "The app cannot see any household lists, plans or recipes." },
    { value: "selected", label: "Only households I choose", hint: "Pick which ones below." },
    { value: "all", label: "All of my households", hint: "Including ones you join later." },
  ];

  return (
    <fieldset>
      <legend className="text-sm font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">Households</legend>
      <div className="mt-2 space-y-2">
        {choices.map((choice) => {
          const allowed = rank[choice.value] <= rank[ceilingMode];
          return (
            <label key={choice.value} className={`flex items-start gap-3 text-sm ${allowed ? "cursor-pointer" : "cursor-not-allowed opacity-40"}`}>
              <input
                type="radio"
                name={name}
                className="mt-1"
                checked={mode === choice.value}
                disabled={!allowed}
                onChange={() => onChange(choice.value, choice.value === "selected" ? selected : [])}
              />
              <span>
                <span className="block text-charcoal-blue-900 dark:text-charcoal-blue-100">{choice.label}</span>
                <span className="block text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">{choice.hint}</span>
              </span>
            </label>
          );
        })}
      </div>

      {mode === "selected" && (
        <div className="mt-3 space-y-2 border-l border-charcoal-blue-200 pl-4 dark:border-white/15">
          {offered.length === 0 && (
            <p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">You are not in any household yet.</p>
          )}
          {offered.map((household) => (
            <label key={household.id} className="flex cursor-pointer items-center gap-3 text-sm text-charcoal-blue-900 dark:text-charcoal-blue-100">
              <input
                type="checkbox"
                checked={selected.includes(household.id)}
                onChange={(event) =>
                  onChange(
                    "selected",
                    event.target.checked ? [...selected, household.id] : selected.filter((id) => id !== household.id),
                  )
                }
              />
              {household.name}
            </label>
          ))}
        </div>
      )}
    </fieldset>
  );
}
