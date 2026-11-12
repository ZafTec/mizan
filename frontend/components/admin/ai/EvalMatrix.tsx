"use client";

import { useState } from "react";
import { Pill } from "@/components/ui/data-table";
import type { AiEvalMatrix, AiEvalRun } from "@/data/admin/ai";

const PASSED = 0;
const FAILED = 1;

function currency(micros: number) {
  return `$${(micros / 1_000_000).toFixed(4)}`;
}

function Outcome({ run }: { run: AiEvalRun | undefined }) {
  if (!run) {
    return <Pill className="shrink-0">not run</Pill>;
  }

  const label =
    run.outcome === PASSED
      ? "passed"
      : run.outcome === FAILED
        ? "failed"
        : "errored";

  return (
    <Pill
      className="shrink-0"
      tone={
        run.outcome === PASSED
          ? "good"
          : run.outcome === FAILED
            ? "bad"
            : "warn"
      }
    >
      {label}
    </Pill>
  );
}

export default function EvalMatrix({
  matrix,
  loading,
  editable,
}: {
  matrix: AiEvalMatrix | null;
  loading: boolean;
  editable: boolean;
}) {
  const [open, setOpen] = useState<string | null>(null);

  if (loading) {
    return (
      <section className="border-t border-foreground py-4 text-[14px] text-muted-foreground">
        Loading evals…
      </section>
    );
  }

  if (!matrix) {
    return (
      <section className="border-t border-foreground py-4 text-[14px] text-muted-foreground">
        {editable
          ? "No results for this text yet. Run the suite."
          : "No results recorded for this version."}
      </section>
    );
  }

  const runs = new Map(matrix.runs.map((r) => [r.caseId, r]));
  const delta =
    matrix.publishedCostMicros === null
      ? null
      : matrix.costMicros - matrix.publishedCostMicros;

  return (
    <section className="flex flex-col">
      <div className="log-section-header flex-wrap">
        <h2 className="text-[16px] font-semibold">Evals</h2>
        <span className="text-[12px] text-muted-foreground">
          {currency(matrix.costMicros)} to run
          {delta !== null && matrix.costMicros > 0 && (
            <>
              {" · "}
              <span
                className={
                  delta > 0
                    ? "text-amber-700 dark:text-amber-300"
                    : "text-verdigris-700 dark:text-verdigris-300"
                }
              >
                {delta > 0 ? "+" : ""}
                {currency(delta)} vs live
              </span>
            </>
          )}
        </span>
      </div>

      {matrix.blockedReason && (
        <p className="border-b border-border py-3 text-[14px] text-tuscan-sun-700 dark:text-tuscan-sun-400">
          {matrix.blockedReason}
        </p>
      )}

      <ul>
        {matrix.cases.map((evalCase) => {
          const run = runs.get(evalCase.id);
          const expanded = open === evalCase.id;

          return (
            <li key={evalCase.id} className="border-b border-border">
              <button
                type="button"
                onClick={() => setOpen(expanded ? null : evalCase.id)}
                aria-expanded={expanded}
                className="flex min-h-11 w-full items-center gap-3 py-2 text-left transition-colors hover:bg-muted/60"
              >
                {evalCase.isAdversarial && (
                  <span
                    title="Gates publish"
                    className="shrink-0 text-[12px] font-semibold"
                  >
                    Gate
                  </span>
                )}
                <span className="min-w-0 flex-1 text-sm">{evalCase.name}</span>
                {run && (
                  <span className="hidden text-[12px] text-muted-foreground tabular-nums sm:inline">
                    {run.tokens} tok · {run.latencyMs} ms
                  </span>
                )}
                <Outcome run={run} />
              </button>

              {expanded && (
                <div className="space-y-2 pb-3 text-xs">
                  <Field label="Input">{evalCase.input}</Field>
                  {evalCase.context && (
                    <Field label="Context">{evalCase.context}</Field>
                  )}
                  <Field label="Assertions">{evalCase.assertions}</Field>
                  {run?.failureReason && (
                    <Field label="Why it failed">{run.failureReason}</Field>
                  )}
                  {run?.output && <Field label="Output">{run.output}</Field>}
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}

function Field({ label, children }: { label: string; children: string }) {
  return (
    <div>
      <span className="text-[12px] font-medium text-muted-foreground">
        {label}
      </span>
      <pre className="mt-0.5 max-h-40 overflow-auto whitespace-pre-wrap font-mono text-[11px] leading-relaxed text-charcoal-blue-700 dark:text-charcoal-blue-300">
        {children}
      </pre>
    </div>
  );
}
