"use client";

import { useCallback, useEffect, useState } from "react";
import { useSession } from "@/lib/auth-client";
import { useSubscription } from "@/lib/hooks/useSubscription";
import { openCheckout, getBillingPortal, PADDLE_PRICES, isPaddleConfigured } from "@/lib/paddle";
import { appToast } from "@/lib/toast";
import { Icon } from "@/components/ui/icon";
import Loading from "@/components/Loading";

const PRO_PLAN = {
  id: "pro",
  name: "Pro",
  price: "$2.99",
  cadence: "per month",
  priceId: () => PADDLE_PRICES.proMonthly,
  blurb: "7-day free trial. Cancel anytime.",
};

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
  const [portalAction, setPortalAction] = useState<"overview" | "payment" | "cancel" | null>(null);

  // Every portal link is single-use, so it is minted right before opening and
  // never held in state - only which button is mid-request, for the spinner.
  const openPortal = useCallback(async (action: "overview" | "payment" | "cancel") => {
    setPortalAction(action);
    try {
      const session = await getBillingPortal();
      const url =
        action === "payment"
          ? session.updatePaymentMethodUrl
          : action === "cancel"
            ? session.cancelSubscriptionUrl
            : session.overviewUrl;

      if (!url) {
        appToast.error("That option isn't available for a lifetime plan.");
        return;
      }

      window.open(url, "_blank", "noopener,noreferrer");
    } catch (error) {
      appToast.error(error, "Could not reach Paddle. Try again in a moment.");
    } finally {
      setPortalAction(null);
    }
  }, []);

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
    const priceId = checkout === "pro" ? PADDLE_PRICES.proMonthly : undefined;
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
            <span className="flex h-14 w-14 shrink-0 items-center justify-center border border-charcoal-blue-200 text-verdigris-700 dark:border-charcoal-blue-700 dark:text-verdigris-400">
              <Icon name="sparkles" size={24} aria-hidden="true" />
            </span>
            <div className="flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="text-xl font-medium tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-2xl">
                  {subscription?.isLifetime ? "Lifetime Pro, forever" : "You're on Pro"}
                </h2>
                <span className="inline-flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.1em] text-verdigris-700 dark:text-verdigris-400">
                  <span className="h-1.5 w-1.5 rounded-full bg-current" aria-hidden="true" />
                  {subscription?.status}
                </span>
              </div>
              <p className="mt-1 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-400">
                {subscription?.isLifetime
                  ? "Thanks for being a founding member. Every feature we ship next is already yours."
                  : "Thanks for supporting Mizan. Every Pro feature is unlocked on your account."}
              </p>
              {!subscription?.isLifetime && subscription?.status === "trialing" && trialEnd && (
                <p className="mt-2 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-400">Trial ends {trialEnd}, then billing starts automatically.</p>
              )}
              {!subscription?.isLifetime && subscription?.status !== "trialing" && canceled && periodEnd && (
                <p className="mt-2 text-sm text-tuscan-sun-700 dark:text-tuscan-sun-400">Cancels {periodEnd}. You keep Pro until then.</p>
              )}
              {!subscription?.isLifetime && subscription?.status !== "trialing" && !canceled && periodEnd && (
                <p className="mt-2 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-400">Renews {periodEnd}.</p>
              )}
              <ul className="mt-4 grid grid-cols-1 gap-2 sm:grid-cols-2">
                {["Unlimited meal plans & shopping lists", "AI coach + food-photo logging", "Household sharing (up to 6)"].map((perk) => (
                  <li key={perk} className="flex items-center gap-2 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
                    <Icon name="circleCheck" size={14} className="text-verdigris-700 dark:text-verdigris-400" aria-hidden="true" />
                    {perk}
                  </li>
                ))}
              </ul>

              <div className="mt-6 flex flex-wrap items-center gap-2 border-t border-charcoal-blue-200 pt-5 dark:border-charcoal-blue-700">
                <button
                  type="button"
                  onClick={() => openPortal("overview")}
                  disabled={portalAction !== null}
                  className="btn-secondary btn-sm"
                >
                  {portalAction === "overview" ? <Loading size="sm" /> : "Manage billing"}
                </button>
                {!subscription?.isLifetime && (
                  <button
                    type="button"
                    onClick={() => openPortal("payment")}
                    disabled={portalAction !== null}
                    className="btn-secondary btn-sm"
                  >
                    {portalAction === "payment" ? <Loading size="sm" /> : "Update payment method"}
                  </button>
                )}
                {!subscription?.isLifetime && !canceled && (
                  <button
                    type="button"
                    onClick={() => openPortal("cancel")}
                    disabled={portalAction !== null}
                    className="btn-ghost btn-sm"
                  >
                    {portalAction === "cancel" ? <Loading size="sm" /> : "Cancel subscription"}
                  </button>
                )}
              </div>
            </div>
          </div>
        </div>
      ) : (
        <div className="card mx-auto max-w-sm p-6 sm:p-8">
          <h3 className="text-lg font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
            {PRO_PLAN.name}
          </h3>
          <div className="mt-2 flex items-baseline gap-2">
            <span className="num text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">{PRO_PLAN.price}</span>
            <span className="text-xs uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">{PRO_PLAN.cadence}</span>
          </div>
          <p className="mt-3 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-400">{PRO_PLAN.blurb}</p>
          <button
            type="button"
            disabled={checkingOut !== null}
            onClick={() => startCheckout(PRO_PLAN.priceId(), PRO_PLAN.id)}
            className="btn-primary mt-5 w-full"
          >
            {checkingOut === PRO_PLAN.id ? <Loading size="sm" /> : "Go Pro"}
          </button>
          <p className="mt-4 text-center text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
            Would rather run it yourself? <a href="https://github.com/ZafTec/mizan#self-hosting" className="footer-link underline">Read the setup guide</a>.
          </p>
        </div>
      )}

      <p className="text-center text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
        14-day refund. Cancel from your account in two clicks. Prices in USD.
      </p>
    </div>
  );
}
