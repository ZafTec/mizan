"use client";

import { useEffect, useId, useRef, useState } from "react";
import Link from "next/link";
import { getAiConsent, type AiConsent } from "@/lib/api/ai";
import {
  discardDraft,
  generateMealPlan,
  readPlanNeedsFoods,
  type GenerateMealPlanResult,
  type PlanNeedsFoodsError,
} from "@/lib/api/mealPlans";
import { readAiProblem, type AiProblem } from "@/lib/ai/problem";
import {
  emptyDraftForm,
  MAX_DRAFT_DAYS,
  parseDraftForm,
  type DraftFormErrors,
  type DraftFormValues,
} from "@/lib/planning/draft";
import { getErrorMessage } from "@/lib/toast";
import DraftPreview from "./DraftPreview";
import PlanRefusal from "./PlanRefusal";

function resetTime(iso: string | null): string | null {
  if (!iso) return null;
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return null;
  return date.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
}

/**
 * "Draft with the assistant" beside the manual form on /meal-plan/new. The
 * person gives the dates, the length, a weekly budget and what they would
 * like; the API drafts and saves the plan, and the preview shows exactly what
 * it answered. A spent allowance and an unavailable provider are said here,
 * in the API's words, and a refusal for want of foods replaces the form with
 * the ways forward (PlanRefusal). Without consent to use the log the action is replaced
 * by the assistant's own way to grant it (docs/PLANNING.md#web).
 */
export default function AssistantDraft({ today }: { today: string }) {
  const id = useId();
  const [consent, setConsent] = useState<AiConsent | null>(null);
  const [consentFailed, setConsentFailed] = useState(false);
  const [open, setOpen] = useState(false);
  const [values, setValues] = useState<DraftFormValues>(() =>
    emptyDraftForm(today),
  );
  const [errors, setErrors] = useState<DraftFormErrors>({});
  const [pending, setPending] = useState(false);
  const [problem, setProblem] = useState<AiProblem | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<GenerateMealPlanResult | null>(null);
  const [refusal, setRefusal] = useState<PlanNeedsFoodsError | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  // One key per request body: trying the same draft again after a lost answer
  // or a 503 reuses it, a changed form gets a new one.
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const firstField = useRef<HTMLInputElement>(null);
  const previewHeading = useRef<HTMLHeadingElement>(null);
  const noticeRef = useRef<HTMLParagraphElement>(null);
  const openButton = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    getAiConsent()
      .then(setConsent)
      .catch(() => setConsentFailed(true));
  }, []);

  useEffect(() => {
    if (result?.drafts.length) previewHeading.current?.focus();
  }, [result]);

  // The notice takes focus once it is on screen. A frame scheduled from the
  // discard handler could run before React committed it and find nothing.
  useEffect(() => {
    if (notice) noticeRef.current?.focus();
  }, [notice]);

  const set = (key: keyof DraftFormValues) => (value: string) =>
    setValues((current) => ({ ...current, [key]: value }));

  const draft = async () => {
    setError(null);
    setProblem(null);
    setNotice(null);
    const parsed = parseDraftForm(values);
    setErrors(parsed.errors ?? {});
    if (!parsed.request) return;
    const body = JSON.stringify(parsed.request);
    if (attempt.current?.body !== body)
      attempt.current = { body, key: crypto.randomUUID() };
    setPending(true);
    try {
      const answer = await generateMealPlan(
        parsed.request,
        attempt.current.key,
      );
      attempt.current = null;
      setResult(answer);
    } catch (caught) {
      const needsFoods = readPlanNeedsFoods(caught);
      if (needsFoods) {
        // A refusal is an answer: the same request would be refused again.
        attempt.current = null;
        setRefusal(needsFoods);
        return;
      }
      const known = readAiProblem(caught);
      // A spent allowance is an answer, not a lost one: asking again later is a new request.
      if (known?.kind === "quota") attempt.current = null;
      if (known) setProblem(known);
      else setError(getErrorMessage(caught, "Could not draft a plan."));
    } finally {
      setPending(false);
    }
  };

  // The confirmation dialog shows a failure itself and stays open.
  const discard = async (planId: string) => {
    await discardDraft(planId);
    const rest = (result?.drafts ?? []).filter(
      (item) => item.plan.id !== planId,
    );
    setResult((current) =>
      current && rest.length > 0 ? { ...current, drafts: rest } : null,
    );
    if (rest.length > 0) return;
    setNotice("Draft discarded.");
  };

  const heading = (
    <header className="log-section-header">
      <h2 id={`${id}-title`}>Draft with the assistant</h2>
    </header>
  );

  if (consent === null && !consentFailed) return null;

  if (!consent?.enabled)
    return (
      <section aria-labelledby={`${id}-title`} data-testid="assistant-draft">
        {heading}
        <p className="mt-3 text-sm text-muted-foreground" data-testid="ai-consent-prompt">
          The assistant can draft a plan from your goal, recipes and local
          prices once you let it use your log.{" "}
          <Link href="/profile/settings#assistant" className="text-link">
            Choose what the assistant can see
          </Link>
        </p>
      </section>
    );

  return (
    <section
      aria-labelledby={`${id}-title`}
      className="space-y-5"
      data-testid="assistant-draft"
    >
      {heading}

      {notice && (
        <p
          ref={noticeRef}
          tabIndex={-1}
          role="status"
          className="text-sm focus-visible:outline-none"
        >
          {notice}
        </p>
      )}

      {result ? (
        <div className="space-y-10">
          {result.drafts.map((item, index) => (
            <DraftPreview
              key={item.plan.id}
              draft={item}
              headingRef={index === 0 ? previewHeading : undefined}
              onDiscard={discard}
            />
          ))}
          {result.skipped.length > 0 && (
            <ul aria-label="Budgets not drafted" className="text-sm">
              {result.skipped.map((skipped, index) => (
                <li key={index} className="ledger-row min-h-0!">
                  <span className="tabular-nums whitespace-nowrap font-medium">
                    {skipped.budgetCeiling.toLocaleString()}
                  </span>
                  <span className="min-w-0 flex-1 text-muted-foreground">
                    {skipped.reason}
                  </span>
                </li>
              ))}
            </ul>
          )}
          {error && (
            <p role="alert" className="input-error text-sm">
              {error}
            </p>
          )}
        </div>
      ) : refusal ? (
        <PlanRefusal
          refusal={refusal}
          request={values}
          onChange={() => {
            setRefusal(null);
            requestAnimationFrame(() => firstField.current?.focus());
          }}
        />
      ) : !open ? (
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">
            Give the dates and a budget, and the assistant drafts the days for
            you to keep or discard.
          </p>
          <button
            ref={openButton}
            type="button"
            className="btn-secondary min-h-11 w-full sm:w-auto"
            onClick={() => {
              setOpen(true);
              requestAnimationFrame(() => firstField.current?.focus());
            }}
          >
            Draft with the assistant
          </button>
        </div>
      ) : (
        <form
          noValidate
          className="space-y-5"
          aria-busy={pending || undefined}
          onSubmit={(event) => {
            event.preventDefault();
            void draft();
          }}
        >
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor={`${id}-start`} className="label">
                Start date
              </label>
              <input
                ref={firstField}
                id={`${id}-start`}
                type="date"
                className="input w-full"
                value={values.startDate}
                onChange={(event) => set("startDate")(event.target.value)}
                aria-invalid={errors.startDate ? true : undefined}
              />
              {errors.startDate && (
                <p className="input-error mt-1 text-sm">{errors.startDate}</p>
              )}
            </div>
            <div>
              <label htmlFor={`${id}-days`} className="label">
                Days
              </label>
              <input
                id={`${id}-days`}
                type="number"
                inputMode="numeric"
                min={1}
                max={MAX_DRAFT_DAYS}
                className="input tabular-nums w-full"
                value={values.durationDays}
                onChange={(event) => set("durationDays")(event.target.value)}
                aria-invalid={errors.durationDays ? true : undefined}
              />
              {errors.durationDays && (
                <p className="input-error mt-1 text-sm">
                  {errors.durationDays}
                </p>
              )}
            </div>
          </div>
          <div>
            <label htmlFor={`${id}-budget`} className="label">
              Weekly budget (optional)
            </label>
            <input
              id={`${id}-budget`}
              type="text"
              inputMode="decimal"
              className="input tabular-nums w-full"
              value={values.weeklyBudget}
              onChange={(event) => set("weeklyBudget")(event.target.value)}
              aria-describedby={`${id}-budget-hint`}
              aria-invalid={errors.weeklyBudget ? true : undefined}
            />
            <p id={`${id}-budget-hint`} className="mt-1 text-xs text-muted-foreground">
              In your price region&apos;s currency.
            </p>
            {errors.weeklyBudget && (
              <p className="input-error mt-1 text-sm">{errors.weeklyBudget}</p>
            )}
          </div>
          <div>
            <label htmlFor={`${id}-daily-budget`} className="label">
              Daily budget (optional)
            </label>
            <input
              id={`${id}-daily-budget`}
              type="text"
              inputMode="decimal"
              className="input tabular-nums w-full"
              value={values.dailyBudget}
              onChange={(event) => set("dailyBudget")(event.target.value)}
              aria-describedby={`${id}-budget-hint`}
              aria-invalid={errors.dailyBudget ? true : undefined}
            />
            {errors.dailyBudget && (
              <p className="input-error mt-1 text-sm">{errors.dailyBudget}</p>
            )}
          </div>
          <div>
            <label htmlFor={`${id}-preference`} className="label">
              What you would like (optional)
            </label>
            <textarea
              id={`${id}-preference`}
              rows={2}
              className="input w-full"
              value={values.preference}
              placeholder="High protein, no pork, cook twice a week"
              onChange={(event) => set("preference")(event.target.value)}
              aria-invalid={errors.preference ? true : undefined}
            />
            {errors.preference && (
              <p className="input-error mt-1 text-sm">{errors.preference}</p>
            )}
          </div>
          <div>
            <label htmlFor={`${id}-name`} className="label">
              Name (optional)
            </label>
            <input
              id={`${id}-name`}
              type="text"
              className="input w-full"
              value={values.name}
              onChange={(event) => set("name")(event.target.value)}
              aria-invalid={errors.name ? true : undefined}
            />
            {errors.name && (
              <p className="input-error mt-1 text-sm">{errors.name}</p>
            )}
          </div>

          {pending && (
            <p role="status" className="text-sm text-muted-foreground">
              The assistant is drafting your plan. This can take a minute.
            </p>
          )}

          {problem && (
            <div role="alert" className="space-y-1 text-sm">
              <p className="input-error">{problem.message}</p>
              {problem.kind === "quota" && resetTime(problem.resetsAt) && (
                <p className="text-muted-foreground">
                  Your planning allowance resets at{" "}
                  {resetTime(problem.resetsAt)}.
                </p>
              )}
              {problem.kind === "unavailable" && (
                <p className="text-muted-foreground">
                  Nothing was saved. Try again in a moment.
                </p>
              )}
            </div>
          )}
          {error && (
            <p role="alert" className="input-error text-sm">
              {error}
            </p>
          )}

          <div className="flex flex-wrap gap-3">
            <button
              type="submit"
              className="btn-secondary min-h-11 flex-1 sm:flex-none"
              disabled={pending}
            >
              {pending
                ? "Drafting…"
                : problem?.kind === "unavailable"
                  ? "Try again"
                  : "Draft plan"}
            </button>
            <button
              type="button"
              className="btn-ghost min-h-11 flex-1 sm:flex-none"
              disabled={pending}
              onClick={() => {
                setOpen(false);
                setProblem(null);
                setError(null);
                requestAnimationFrame(() => openButton.current?.focus());
              }}
            >
              Cancel
            </button>
          </div>
        </form>
      )}
    </section>
  );
}
