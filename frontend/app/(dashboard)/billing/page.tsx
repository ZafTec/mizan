"use client";

import { useCallback, useEffect, useState } from "react";
import { useSession } from "@/lib/auth-client";
import { useSubscription } from "@/lib/hooks/useSubscription";
import { openCheckout, PADDLE_PRICES, isPaddleConfigured } from "@/lib/paddle";
import { appToast } from "@/lib/toast";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import Loading from "@/components/Loading";

const PLANS = [
  { id: "pro", name: "Pro Monthly", price: "$1.99", cadence: "per month", priceId: () => PADDLE_PRICES.proMonthly, blurb: "7-day free trial. Cancel anytime." },
  { id: "pro-yearly", name: "Pro Yearly", price: "$15", cadence: "per year", priceId: () => PADDLE_PRICES.proYearly, blurb: "Two months free vs monthly.", highlight: true },
  { id: "lifetime", name: "Lifetime", price: "$48", cadence: "one-time", priceId: () => PADDLE_PRICES.lifetime, blurb: "Pay once. Pro forever, plus every future feature." },
];

function formatDate(value: string | null): string | null {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

export default function BillingPage() {
  const { data: session } = useSession();
  const user = session?.user;
  const { subscription, isPro, loading, refresh } = useSubscription();
  const [checkingOut, setCheckingOut] = useState<string | null>(null);
  const [awaitingActivation, setAwaitingActivation] = useState(false);

  const startCheckout = useCallback(async (priceId: string, planId: string) => {
    if (!user?.id) {
      appToast.error("Please sign in first");
      return;
    }
    if (!priceId || !isPaddleConfigured()) {
      appToast.error("Billing is not configured yet");
      return;
    }

    setCheckingOut(planId);
    const opened = await openCheckout({
      priceId,
      userId: user.id,
      email: user.email ?? undefined,
      eventCallback: (event) => {
        const name = String(event?.name ?? "");
        if (name === "checkout.completed") {
          setAwaitingActivation(true);
        } else if (name === "checkout.closed") {
          setCheckingOut(null);
        }
      },
    });

    if (!opened) {
      appToast.error("Could not open checkout");
      setCheckingOut(null);
    }
  }, [user]);

  // After checkout completes, Paddle provisions the subscription asynchronously
  // via webhook. Poll our own endpoint until the entitlement flips.
  useEffect(() => {
    if (!awaitingActivation) return;
    let tries = 0;
    const interval = setInterval(async () => {
      tries += 1;
      await refresh();
      if (tries >= 12) {
        clearInterval(interval);
        setAwaitingActivation(false);
        setCheckingOut(null);
      }
    }, 3000);
    return () => clearInterval(interval);
  }, [awaitingActivation, refresh]);

  useEffect(() => {
    if (isPro && awaitingActivation) {
      setAwaitingActivation(false);
      setCheckingOut(null);
      appToast.success("You're on Pro. Welcome aboard.");
    }
  }, [isPro, awaitingActivation]);

  // Auto-open checkout when arriving from a pricing CTA.
  useEffect(() => {
    if (!user?.id) return;
    const checkout = new URLSearchParams(window.location.search).get("checkout");
    if (!checkout) return;
    const map: Record<string, string> = {
      pro: PADDLE_PRICES.proMonthly,
      "pro-yearly": PADDLE_PRICES.proYearly,
      lifetime: PADDLE_PRICES.lifetime,
    };
    const priceId = map[checkout];
    if (priceId) {
      window.history.replaceState({}, "", "/billing");
      startCheckout(priceId, checkout);
    }
  }, [user, startCheckout]);

  const periodEnd = formatDate(subscription?.currentPeriodEnd ?? null);
  const trialEnd = formatDate(subscription?.trialEndsAt ?? null);
  const canceled = Boolean(subscription?.canceledAt);

  return (
    <div className="mx-auto max-w-4xl space-y-8">
      <header>
        <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">Billing</h1>
        <p className="mt-1 text-charcoal-blue-500 dark:text-charcoal-blue-400">Manage your Mizan plan. Billing is handled securely by Paddle.</p>
      </header>

      {awaitingActivation && (
        <div className="flex items-center gap-3 rounded-2xl border border-brand-500/30 bg-brand-50 p-4 text-sm text-brand-800 dark:bg-brand-950 dark:text-brand-200">
          <Loading size="sm" />
          Provisioning your subscription. This usually takes a few seconds.
        </div>
      )}

      {loading ? (
        <div className="flex justify-center py-16"><Loading /></div>
      ) : isPro ? (
        <div className="card p-6 sm:p-8">
          <div className="flex items-start gap-4">
            <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-brand-600 text-white dark:bg-brand-500">
              <AnimatedIcon name="sparkles" size={22} aria-hidden="true" />
            </span>
            <div className="flex-1">
              <h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
                {subscription?.isLifetime ? "Lifetime — Pro forever" : "Pro plan active"}
              </h2>
              <p className="mt-1 text-sm capitalize text-charcoal-blue-500 dark:text-charcoal-blue-400">
                Status: {subscription?.status}
              </p>
              {!subscription?.isLifetime && subscription?.status === "trialing" && trialEnd && (
                <p className="mt-1 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">Trial ends {trialEnd}.</p>
              )}
              {!subscription?.isLifetime && canceled && periodEnd && (
                <p className="mt-1 text-sm text-burnt-peach-700 dark:text-burnt-peach-300">Cancels on {periodEnd}. You keep Pro until then.</p>
              )}
              {!subscription?.isLifetime && !canceled && periodEnd && (
                <p className="mt-1 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">Renews {periodEnd}.</p>
              )}
            </div>
          </div>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          {PLANS.map((plan) => (
            <article
              key={plan.id}
              className={`relative flex flex-col rounded-[24px] border p-6 ${
                plan.highlight
                  ? "border-brand-500/30 bg-white shadow-xl shadow-brand-500/10 dark:bg-charcoal-blue-900"
                  : "border-charcoal-blue-100 bg-white dark:border-white/10 dark:bg-charcoal-blue-900/60"
              }`}
            >
              <h3 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{plan.name}</h3>
              <div className="mt-2 flex items-baseline gap-2">
                <span className="text-3xl font-bold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">{plan.price}</span>
                <span className="text-xs uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">{plan.cadence}</span>
              </div>
              <p className="mt-3 flex-1 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-400">{plan.blurb}</p>
              <button
                type="button"
                disabled={checkingOut !== null}
                onClick={() => startCheckout(plan.priceId(), plan.id)}
                className={`mt-5 w-full ${plan.highlight ? "btn-primary" : "btn-secondary"}`}
              >
                {checkingOut === plan.id ? <Loading size="sm" /> : "Choose"}
              </button>
            </article>
          ))}
        </div>
      )}

      <p className="text-center text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
        14-day refund on every paid plan. Prices in USD.
      </p>
    </div>
  );
}
