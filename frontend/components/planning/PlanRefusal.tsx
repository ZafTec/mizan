"use client";

import { useEffect, useRef } from "react";
import Link from "next/link";
import { ChevronRight, TriangleAlert } from "lucide-react";
import type { PlanNeedsFoodsError } from "@/lib/api/mealPlans";
import type { DraftFormValues } from "@/lib/planning/draft";

/**
 * What the assistant draft says when the API refused it for want of foods
 * (design board PlanRefusal.dc.html): the API's sentence, the ways forward
 * that exist today, and the request as the person typed it. Nothing was
 * saved, so "Change request" goes back to the same form with the same values.
 */
export default function PlanRefusal({
  refusal,
  request,
  onChange,
}: {
  refusal: PlanNeedsFoodsError;
  request: DraftFormValues;
  onChange: () => void;
}) {
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => heading.current?.focus(), []);

  const asked: Array<[string, string]> = [
    ["Start date", request.startDate],
    ["Days", request.durationDays],
    ["Weekly budget", request.weeklyBudget],
    ["Daily budget", request.dailyBudget],
    ["What you would like", request.preference],
    ["Name", request.name],
  ].filter((row): row is [string, string] => Boolean(row[1]?.trim()));

  return (
    <div
      className="grid gap-6 md:grid-cols-[minmax(0,1fr)_260px]"
      data-testid="plan-refusal"
    >
      <div className="space-y-5 border-l-2 border-tuscan-sun-700 pl-4 dark:border-tuscan-sun-400">
        <div className="flex gap-3" role="alert">
          <TriangleAlert
            aria-hidden="true"
            size={22}
            strokeWidth={1.6}
            className="mt-0.5 shrink-0 text-tuscan-sun-700 dark:text-tuscan-sun-400"
          />
          <div className="min-w-0 space-y-1.5">
            <h3
              ref={heading}
              tabIndex={-1}
              className="text-[17px] font-semibold focus-visible:outline-none"
            >
              Not enough foods for this plan
            </h3>
            <p className="text-sm">{refusal.error}</p>
            <p className="text-sm text-muted-foreground">Nothing was saved.</p>
          </div>
        </div>

        {(refusal.emptyDays.length > 0 || refusal.missingFoods.length > 0) && (
          <dl className="space-y-3 text-sm">
            {refusal.emptyDays.length > 0 && (
              <div>
                <dt className="text-[10px] font-semibold tracking-[0.14em] uppercase text-muted-foreground">
                  Days without a food
                </dt>
                <dd className="tabular-nums mt-1">
                  {refusal.emptyDays.map((day) => `Day ${day + 1}`).join(", ")}
                </dd>
              </div>
            )}
            {refusal.missingFoods.length > 0 && (
              <div>
                <dt className="text-[10px] font-semibold tracking-[0.14em] uppercase text-muted-foreground">
                  Not in your foods
                </dt>
                <dd className="mt-1">{refusal.missingFoods.join(", ")}</dd>
              </div>
            )}
          </dl>
        )}

        <nav aria-label="Ways forward">
          <p className="text-[10px] font-semibold tracking-[0.14em] uppercase text-muted-foreground">
            Ways forward
          </p>
          <ul className="mt-1">
            <li>
              <Way
                href="/ingredients"
                title="Add foods"
                detail={
                  refusal.missingFoods.length > 0
                    ? `Start with ${refusal.missingFoods.slice(0, 3).join(", ")}`
                    : "Add the foods you eat to your catalogue"
                }
              />
            </li>
            <li>
              <button
                type="button"
                onClick={onChange}
                className="ledger-row w-full text-left transition-colors hover:bg-hair"
              >
                <WayText
                  title="Loosen the rules"
                  detail="Change the days, budget or what you asked for"
                />
              </button>
            </li>
            <li>
              <Way
                href="#manual-plan"
                title="Build the plan by hand"
                detail="Start from an empty plan below"
              />
            </li>
          </ul>
        </nav>
      </div>

      <aside
        aria-label="Your request"
        className="self-start border-t border-foreground pt-3"
      >
        <p className="text-[10px] font-semibold tracking-[0.14em] uppercase text-muted-foreground">
          You asked for
        </p>
        <dl className="mt-1 text-sm">
          {asked.map(([label, value]) => (
            <div
              key={label}
              className="flex justify-between gap-3 border-b border-border py-2"
            >
              <dt className="text-muted-foreground">{label}</dt>
              <dd className="min-w-0 text-right break-words">{value}</dd>
            </div>
          ))}
        </dl>
        <button
          type="button"
          onClick={onChange}
          className="btn-secondary mt-4 min-h-11 w-full"
        >
          Change request
        </button>
      </aside>
    </div>
  );
}

function WayText({ title, detail }: { title: string; detail: string }) {
  return (
    <>
      <span className="min-w-0 flex-1">
        <span className="block text-[15px] font-medium">{title}</span>
        <span className="mt-0.5 block text-[13px] text-muted-foreground">{detail}</span>
      </span>
      <ChevronRight
        aria-hidden="true"
        size={18}
        strokeWidth={1.6}
        className="text-muted-foreground"
      />
    </>
  );
}

function Way({
  href,
  title,
  detail,
}: {
  href: string;
  title: string;
  detail: string;
}) {
  return (
    <Link href={href} className="ledger-row transition-colors hover:bg-hair">
      <WayText title={title} detail={detail} />
    </Link>
  );
}
