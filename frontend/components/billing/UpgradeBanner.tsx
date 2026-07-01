"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { useSubscription } from "@/lib/hooks/useSubscription";

interface UpgradeBannerProps {
  /** Unique id for dismiss persistence; use a different id per placement. */
  id: string;
  title: string;
  message: string;
  checkoutPlan?: "pro" | "pro-yearly" | "lifetime";
  /** "hero" for large placements (dashboard), "compact" for inline nudges. */
  variant?: "hero" | "compact";
}

function dismissKey(id: string) {
  return `upgrade-banner-dismissed:${id}`;
}

export function UpgradeBanner({ id, title, message, checkoutPlan = "pro", variant = "compact" }: UpgradeBannerProps) {
  const { isPro, loading } = useSubscription();
  const [dismissed, setDismissed] = useState(true);

  useEffect(() => {
    setDismissed(localStorage.getItem(dismissKey(id)) === "1");
  }, [id]);

  if (loading || isPro || dismissed) {
    return null;
  }

  function dismiss() {
    localStorage.setItem(dismissKey(id), "1");
    setDismissed(true);
  }

  if (variant === "hero") {
    return (
      <div className="relative overflow-hidden rounded-[28px] border border-brand-500/25 bg-gradient-to-br from-brand-600 to-brand-700 p-6 text-white shadow-lg shadow-brand-500/20 sm:p-8 dark:from-brand-600 dark:to-brand-800">
        <button
          type="button"
          onClick={dismiss}
          aria-label="Dismiss"
          className="absolute right-4 top-4 flex h-8 w-8 items-center justify-center rounded-xl text-white/70 transition-colors hover:bg-white/10 hover:text-white"
        >
          <AnimatedIcon name="x" size={14} />
        </button>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="max-w-xl space-y-2 pr-8">
            <div className="inline-flex items-center gap-1.5 rounded-full bg-white/15 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.14em]">
              <AnimatedIcon name="sparkles" size={12} />
              Mizan Pro
            </div>
            <h3 className="text-xl font-semibold tracking-tight sm:text-2xl">{title}</h3>
            <p className="text-sm text-white/85">{message}</p>
          </div>
          <Link
            href={`/billing?checkout=${checkoutPlan}`}
            className="inline-flex shrink-0 items-center justify-center gap-2 rounded-2xl bg-white px-5 py-3 text-sm font-semibold text-brand-700 transition-transform hover:-translate-y-0.5"
          >
            <AnimatedIcon name="sparkles" size={16} />
            Upgrade to Pro
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-start gap-3 rounded-2xl border border-brand-500/25 bg-brand-500/5 p-4 sm:flex-row sm:items-center sm:justify-between dark:bg-brand-500/10">
      <div className="flex items-start gap-3">
        <span className="icon-chip h-9 w-9 shrink-0 text-brand-700 dark:text-brand-300">
          <AnimatedIcon name="sparkles" size={16} />
        </span>
        <div>
          <p className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{title}</p>
          <p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">{message}</p>
        </div>
      </div>
      <div className="flex w-full items-center gap-2 sm:w-auto">
        <Link href={`/billing?checkout=${checkoutPlan}`} className="btn-primary flex-1 !py-2 text-xs sm:flex-none">
          Upgrade
        </Link>
        <button
          type="button"
          onClick={dismiss}
          aria-label="Dismiss"
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl text-charcoal-blue-400 hover:bg-charcoal-blue-100 dark:hover:bg-white/5"
        >
          <AnimatedIcon name="x" size={14} />
        </button>
      </div>
    </div>
  );
}
