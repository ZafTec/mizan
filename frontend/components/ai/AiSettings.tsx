"use client";

import { useCallback, useEffect, useState } from "react";
import Loading from "@/components/Loading";
import { ToggleRow } from "@/components/ui/toggle-row";
import { consentInput } from "@/components/ai/AiPermissionGate";
import {
  getAiConsent,
  getMyAiUsage,
  updateAiConsent,
  type AiConsent,
  type MyAiUsage,
} from "@/lib/api/ai";
import { saveStatusText, type SaveStatus } from "@/lib/hooks/useSaveStatus";
import { appToast } from "@/lib/toast";

/** The same switches the assistant panel asks for (AiPermissionGate), so both read alike. */
const SEE = [
  {
    key: "shareNutrition",
    label: "See meals and meal plans",
    description: "What you ate, your targets, plans and shopping lists.",
  },
  {
    key: "shareTraining",
    label: "See workouts",
    description: "Sessions, sets and programs.",
  },
  {
    key: "shareBody",
    label: "See body measurements",
    description: "Weight, waist and the other measurements you log.",
  },
] as const;

const WRITE = [
  {
    key: "writeNutrition",
    label: "Meals, plans and shopping lists",
    description: "Record what you ate, save targets, build plans and lists.",
  },
  {
    key: "writeTraining",
    label: "Workouts",
    description: "Record sessions, exercises and sets.",
  },
  {
    key: "writeBody",
    label: "Body measurements",
    description: "Record weigh-ins and body measurements.",
  },
] as const;

type SwitchKey =
  | (typeof SEE)[number]["key"]
  | (typeof WRITE)[number]["key"]
  | "act";

const NOTHING: AiConsent = {
  enabled: false,
  shareNutrition: false,
  shareTraining: false,
  shareBody: false,
  allowWrites: false,
  writeNutrition: false,
  writeTraining: false,
  writeBody: false,
};

/**
 * What the assistant may see and do, and what it has cost you. Every switch
 * starts off and saves as it changes; an axis that is off is never sent - see
 * docs/AI.md#consent. Seeing anything turns the master switch on, and turning
 * the last one off turns it off, as the panel's permission step does.
 */
export function AiSettings() {
  const [consent, setConsent] = useState<AiConsent>(NOTHING);
  const [usage, setUsage] = useState<MyAiUsage | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState<SwitchKey | null>(null);
  const [last, setLast] = useState<{ key: SwitchKey; status: SaveStatus }>();

  const load = useCallback(async () => {
    try {
      const [loadedConsent, loadedUsage] = await Promise.all([
        getAiConsent(),
        getMyAiUsage(),
      ]);
      setConsent(loadedConsent);
      setUsage(loadedUsage);
    } catch (error) {
      appToast.error(error, "Could not load your assistant settings");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (last?.status !== "saved") return;
    const timer = window.setTimeout(() => setLast(undefined), 2500);
    return () => window.clearTimeout(timer);
  }, [last]);

  async function save(next: AiConsent, key: SwitchKey) {
    const previous = consent;
    setConsent(next);
    setSaving(key);
    setLast({ key, status: "saving" });
    try {
      setConsent(await updateAiConsent(consentInput(next)));
      setLast({ key, status: "saved" });
    } catch (error) {
      setConsent(previous);
      setLast({ key, status: "failed" });
      appToast.error(error, "Could not save that change");
    } finally {
      setSaving(null);
    }
  }

  if (loading) {
    return (
      <div className="flex justify-center py-8">
        <Loading />
      </div>
    );
  }

  const status = (key: SwitchKey) =>
    last?.key === key ? saveStatusText(last.status) : "";
  const seeing = (key: (typeof SEE)[number]["key"]) =>
    consent.enabled && consent[key];
  const acting =
    consent.allowWrites && WRITE.some((axis) => consent[axis.key]);

  const see = (key: (typeof SEE)[number]["key"], checked: boolean) => {
    const next = {
      ...consent,
      ...(consent.enabled ? {} : { shareNutrition: false, shareTraining: false, shareBody: false }),
      [key]: checked,
    };
    next.enabled = SEE.some((axis) => next[axis.key]);
    void save(next, key);
  };
  const act = (checked: boolean) =>
    save(
      {
        ...consent,
        allowWrites: checked,
        writeNutrition: checked,
        writeTraining: checked,
        writeBody: checked,
      },
      "act",
    );
  const write = (key: (typeof WRITE)[number]["key"], checked: boolean) => {
    const next = { ...consent, [key]: checked };
    next.allowWrites = WRITE.some((axis) => next[axis.key]);
    void save(next, key);
  };

  return (
    <div className="space-y-6">
      <div className="border-b border-border">
        {SEE.map((axis) => (
          <ToggleRow
            key={axis.key}
            label={axis.label}
            description={axis.description}
            checked={seeing(axis.key)}
            disabled={saving !== null}
            status={status(axis.key)}
            onChange={(checked) => see(axis.key, checked)}
          />
        ))}
        <ToggleRow
          label="Log and plan when I ask"
          description="Adds meals, workouts, measurements, plans and lists. It says what it did, and never deletes anything."
          checked={acting}
          disabled={saving !== null}
          status={status("act")}
          onChange={(checked) => void act(checked)}
        />
        <details className="group border-t border-border">
          <summary className="cursor-pointer text-link">
            Choose what it may log
          </summary>
          <div className="pb-3 pl-4">
            {WRITE.map((axis) => (
              <ToggleRow
                key={axis.key}
                label={axis.label}
                description={axis.description}
                checked={consent.allowWrites && consent[axis.key]}
                disabled={saving !== null}
                status={status(axis.key)}
                onChange={(checked) => write(axis.key, checked)}
              />
            ))}
          </div>
        </details>
      </div>

      <p className="text-xs leading-relaxed text-muted-foreground">
        An axis you have not shared is never sent to the model: it is left out
        of the request, not included with an instruction to ignore it. Seeing
        and logging are separate: the assistant can record a meal you dictate
        without seeing your history. With everything off it still answers
        general questions about food and training.
      </p>

      {usage && <UsagePanel usage={usage} />}
    </div>
  );
}

function UsagePanel({ usage }: { usage: MyAiUsage }) {
  const { today } = usage;
  const resets = new Date(today.resetsAt);

  return (
    <section aria-labelledby="ai-usage-title" id="assistant-usage">
      <div className="flex items-baseline justify-between gap-3 border-b border-foreground pb-2">
        <h3 id="ai-usage-title" className="text-sm font-semibold">
          Today&apos;s usage
        </h3>
        <span className="text-xs text-muted-foreground">{today.plan}</span>
      </div>

      <div className="grid gap-4 py-3 sm:grid-cols-2">
        <Meter
          label="Requests"
          used={today.requestsUsed}
          limit={today.requestLimit}
        />
        <Meter
          label="Tokens"
          used={today.tokensUsed}
          limit={today.tokenLimit}
        />
      </div>

      {/* Its own allowance: drafting a plan never spends chat, nor chat a plan. */}
      <div className="border-t border-border py-3" data-testid="ai-usage-planning">
        <Meter
          label="Meal plans drafted"
          used={usage.planning.requestsUsed}
          limit={usage.planning.requestLimit}
        />
      </div>

      {usage.byFeature.length > 0 && (
        <ul className="border-t border-border py-2">
          {usage.byFeature.map((feature) => (
            <li
              key={feature.feature}
              className="tabular-nums flex items-center justify-between gap-3 py-1 text-xs text-muted-foreground"
            >
              <span>{feature.feature}</span>
              <span>
                {feature.requests} request{feature.requests === 1 ? "" : "s"} ·{" "}
                {feature.tokens.toLocaleString()} tokens
              </span>
            </li>
          ))}
        </ul>
      )}

      <p className="border-t border-border pt-2 text-xs text-muted-foreground">
        Resets at{" "}
        {resets.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}.
      </p>
    </section>
  );
}

function Meter({
  label,
  used,
  limit,
}: {
  label: string;
  used: number;
  limit: number;
}) {
  const percent =
    limit > 0 ? Math.min(100, Math.round((used / limit) * 100)) : 0;

  return (
    <div>
      <div className="flex items-baseline justify-between text-xs">
        <span className="text-muted-foreground">{label}</span>
        <span className="tabular-nums font-medium">
          {used.toLocaleString()} / {limit.toLocaleString()}
        </span>
      </div>
      <div className="target-track mt-1.5">
        <span
          className={percent >= 90 ? "bg-destructive" : "bg-brand-700 dark:bg-brand-300"}
          style={{ width: `${percent}%` }}
        />
      </div>
    </div>
  );
}
