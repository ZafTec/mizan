"use client";

import { useState } from "react";
import {
  updateAiConsent,
  type AiConsent,
  type AiConsentRequirement,
} from "@/lib/api/ai";
import { appToast } from "@/lib/toast";
import { cn } from "@/lib/utils";

/**
 * What the assistant may use, asked where it is first needed: the chat panel the first time, and
 * onboarding before it records anything (docs/AI.md#consent). Each switch says in one line what it
 * allows. Planning has no switch of its own; it borrows nutrition's, so that switch is named for
 * both.
 */
const SEE = [
  {
    key: "shareNutrition",
    label: "Meals and meal plans",
    description: "What you ate, your targets, plans and shopping lists.",
  },
  {
    key: "shareTraining",
    label: "Workouts",
    description: "Sessions, sets and programs.",
  },
  {
    key: "shareBody",
    label: "Body measurements",
    description: "Weight, waist and the other measurements you log.",
  },
] as const;

type SeeKey = (typeof SEE)[number]["key"];

export type AiConsentInput = Omit<AiConsent, "updatedAt">;

export function consentInput(consent: AiConsent): AiConsentInput {
  return {
    enabled: consent.enabled,
    shareNutrition: consent.shareNutrition,
    shareTraining: consent.shareTraining,
    shareBody: consent.shareBody,
    allowWrites: consent.allowWrites,
    writeNutrition: consent.writeNutrition,
    writeTraining: consent.writeTraining,
    writeBody: consent.writeBody,
  };
}

/** Whether the person has chosen anything yet; both master switches off is the never-asked state. */
export function consentMissing(consent: AiConsent | null) {
  return consent !== null && !consent.enabled && !consent.allowWrites;
}

const AXIS_KEYS = {
  nutrition: { read: "shareNutrition", write: "writeNutrition" },
  training: { read: "shareTraining", write: "writeTraining" },
  body: { read: "shareBody", write: "writeBody" },
} as const;

/** The consent with just the switch a refusal named turned on, and its master switch with it. */
export function grantRequirement(
  consent: AiConsent,
  requirement: AiConsentRequirement,
): AiConsentInput {
  const keys =
    AXIS_KEYS[requirement.axis as keyof typeof AXIS_KEYS] ??
    AXIS_KEYS.nutrition;
  const next = consentInput(consent);
  if (requirement.access === "write")
    return { ...next, allowWrites: true, [keys.write]: true };
  return { ...next, enabled: true, [keys.read]: true };
}

const AXIS_WORDS: Record<string, { see: string; write: string }> = {
  nutrition: { see: "meals and meal plans", write: "logging meals and plans" },
  training: { see: "workouts", write: "logging workouts" },
  body: { see: "body measurements", write: "logging measurements" },
};

/** How a refusal reads to the person: what is off, and the one button that turns it on. */
export function describeRequirement(requirement: AiConsentRequirement) {
  const words = AXIS_WORDS[requirement.axis] ?? AXIS_WORDS.nutrition;
  return requirement.access === "write"
    ? {
        title: `${capitalise(words.write)} is switched off`,
        action: `Allow ${words.write}`,
      }
    : {
        title: `The assistant can't see your ${words.see}`,
        action: `Allow ${words.see}`,
      };
}

function capitalise(text: string) {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

export default function AiPermissionGate({
  consent,
  onGranted,
  onNotNow,
  title = "What can the assistant use?",
  lead = "It only sees what you switch on. You can change this at any time in Settings.",
  submitLabel = "Allow and start",
  className,
}: {
  consent: AiConsent;
  onGranted: (next: AiConsent) => void;
  /** Leaves everything off. Without it the step has one way on, as onboarding does. */
  onNotNow?: () => void;
  title?: string;
  lead?: string;
  submitLabel?: string;
  className?: string;
}) {
  const asked = !consentMissing(consent);
  const [see, setSee] = useState<Record<SeeKey, boolean>>({
    shareNutrition: asked ? consent.shareNutrition : true,
    shareTraining: asked ? consent.shareTraining : true,
    shareBody: asked ? consent.shareBody : false,
  });
  const [act, setAct] = useState(asked ? consent.allowWrites : true);
  const [saving, setSaving] = useState(false);
  const anything = act || Object.values(see).some(Boolean);

  async function grant() {
    if (!anything && onNotNow) {
      onNotNow();
      return;
    }
    setSaving(true);
    try {
      const next = await updateAiConsent({
        enabled: Object.values(see).some(Boolean),
        ...see,
        allowWrites: act,
        writeNutrition: act,
        writeTraining: act,
        writeBody: act,
      });
      onGranted(next);
    } catch (error) {
      appToast.error(error, "Could not save your choice");
    } finally {
      setSaving(false);
    }
  }

  return (
    <section
      aria-labelledby="ai-permission-title"
      className={cn("flex min-h-0 flex-1 flex-col", className)}
    >
      <div className="min-h-0 flex-1 space-y-5 overflow-y-auto px-5 py-6">
        <div className="space-y-2">
          <h2
            id="ai-permission-title"
            data-autofocus
            tabIndex={-1}
            className="text-xl font-semibold tracking-tight outline-none"
          >
            {title}
          </h2>
          <p className="text-sm leading-relaxed text-muted-foreground">
            {lead}
          </p>
        </div>

        <fieldset>
          <legend className="eyebrow pb-2">It can see</legend>
          <div className="border-b border-border">
            {SEE.map((axis) => (
              <Choice
                key={axis.key}
                label={axis.label}
                description={axis.description}
                checked={see[axis.key]}
                disabled={saving}
                onChange={(checked) =>
                  setSee((current) => ({ ...current, [axis.key]: checked }))
                }
              />
            ))}
          </div>
        </fieldset>

        <fieldset>
          <legend className="eyebrow pb-2">It can do</legend>
          <div className="border-b border-border">
            <Choice
              label="Log and plan when I ask"
              description="Adds meals, workouts, measurements, plans and lists. It says what it did, and never deletes anything."
              checked={act}
              disabled={saving}
              onChange={setAct}
            />
          </div>
        </fieldset>

        <p className="text-xs leading-relaxed text-muted-foreground">
          With everything off, it can still answer general questions about food
          and training.
        </p>
      </div>

      <div className="flex gap-2 border-t border-border px-4 py-3">
        <button
          type="button"
          onClick={grant}
          disabled={saving}
          className="btn-primary min-h-11 flex-1"
        >
          {saving ? "Saving…" : submitLabel}
        </button>
        {onNotNow && (
          <button
            type="button"
            onClick={onNotNow}
            disabled={saving}
            className="btn-secondary min-h-11"
          >
            Not now
          </button>
        )}
      </div>
    </section>
  );
}

function Choice({
  label,
  description,
  checked,
  disabled,
  onChange,
}: {
  label: string;
  description: string;
  checked: boolean;
  disabled?: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className="flex min-h-16 w-full items-center gap-4 border-t border-border py-3.5 text-left disabled:opacity-60"
    >
      <span className="min-w-0 flex-1">
        <span className="block text-[15px] font-medium">{label}</span>
        <span className="mt-0.5 block text-[13px] leading-snug text-muted-foreground">
          {description}
        </span>
      </span>
      <span
        aria-hidden="true"
        className={cn(
          "relative h-[26px] w-11 shrink-0 rounded-full transition-colors motion-reduce:transition-none",
          checked ? "bg-brand-700 dark:bg-brand-300" : "bg-input",
        )}
      >
        <span
          className={cn(
            "absolute top-[3px] h-5 w-5 rounded-full transition-transform motion-reduce:transition-none",
            checked
              ? "translate-x-[21px] bg-white dark:bg-charcoal-blue-950"
              : "translate-x-[3px] bg-foreground",
          )}
        />
      </span>
    </button>
  );
}
