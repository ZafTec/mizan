"use client";

import { useState } from "react";
import type { HardConstraint } from "@/data/admin/ai";

/**
 * The half of the guardrails nobody can edit here, shown next to the half they
 * can. A constraint nobody can see gets worked around by people who do not
 * know it exists (docs/AI.md).
 */
export default function HardConstraints({
  preamble,
  constraints,
}: {
  preamble: string;
  constraints: HardConstraint[];
}) {
  const [showPreamble, setShowPreamble] = useState(false);

  return (
    <section className="flex flex-col">
      <div className="log-section-header flex-wrap">
        <h2 className="text-[16px] font-semibold">Enforced in code</h2>
        <button
          type="button"
          aria-expanded={showPreamble}
          onClick={() => setShowPreamble((v) => !v)}
          className="text-link"
        >
          {showPreamble ? "Hide" : "Show"} preamble
        </button>
      </div>

      <p className="pt-3 text-[12px] text-muted-foreground">
        Not editable here, and not enforced by the prompt below. Changing any of
        these is a code change and a deploy.
      </p>

      <ul className="mt-2">
        {constraints.map((c) => (
          <li key={c.title} className="border-b border-border py-2.5 text-xs">
            <span className="font-medium">{c.title}</span>
            <span className="text-muted-foreground">
              {" — "}
              {c.detail}
            </span>
            <span className="block text-muted-foreground">{c.enforcedBy}</span>
          </li>
        ))}
      </ul>

      {showPreamble && (
        <pre className="mt-3 max-h-64 overflow-auto rounded-[3px] border border-border bg-card p-3 font-mono text-[11px] leading-relaxed whitespace-pre-wrap">
          {preamble}
        </pre>
      )}
    </section>
  );
}
