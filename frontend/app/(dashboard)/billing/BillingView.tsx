"use client";

import { useCallback, useEffect, useState } from "react";
import { useSession } from "@/lib/auth-client";
import { useSubscription } from "@/lib/hooks/useSubscription";
import { clientApi } from "@/lib/api.client";
import { openCheckout, getBillingPortal, getPaddle, isPaddleConfigured } from "@/lib/paddle";
import {
	cadence,
	describeDeal,
	firstErrorMessage,
	formatMoney,
	pickPlans,
	yearlySavingPercent,
	type BillingPlan,
	type BillingTransaction,
	type MySubscription,
	type PlanChangePreview,
} from "@/lib/billing";
import { appToast } from "@/lib/toast";
import { Icon } from "@/components/ui/icon";
import Loading from "@/components/Loading";
import ConfirmationModal from "@/components/ConfirmationModal";
import { ModalShell } from "@/components/ModalShell";

const PRO_PERKS = [
	"Photo analysis: snap a plate, confirm the estimate",
	"A working daily assistant allowance",
	"The Telegram bot",
	"Coach relationships",
];

function formatDate(value: string | null | undefined): string | null {
	if (!value) return null;
	const d = new Date(value);
	return Number.isNaN(d.getTime()) ? null : d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

/**
 * The billing page - docs/ARCHITECTURE.md#billing. A free account picks a plan
 * and checks out in Paddle's overlay. A subscriber sees what they pay and when,
 * switches between monthly and yearly with the cost shown first, cancels or
 * keeps the subscription, and downloads invoices. Card changes go to Paddle's
 * hosted portal, the one thing that must never touch these servers.
 */
export default function BillingView({ plans }: { plans: BillingPlan[] }) {
	const { data: session } = useSession();
	const user = session?.user;
	const { subscription, isPro, loading, refresh } = useSubscription();
	const [awaitingActivation, setAwaitingActivation] = useState(false);
	const [checkingOut, setCheckingOut] = useState<string | null>(null);

	const startCheckout = useCallback(
		async (plan: BillingPlan) => {
			if (!user?.id) {
				appToast.error("Please sign in first");
				return;
			}
			if (!isPaddleConfigured()) {
				appToast.error("Billing is not configured yet");
				return;
			}

			// The first call registers the checkout event handler; Paddle keeps
			// the handler from its first initialisation.
			await getPaddle((event) => {
				const name = String(event?.name ?? "");
				if (name === "checkout.completed") {
					setAwaitingActivation(true);
				} else if (name === "checkout.closed") {
					setCheckingOut(null);
				}
			});
			setCheckingOut(plan.id);
			const opened = await openCheckout({
				priceId: plan.paddlePriceId,
				discountId: plan.deal?.paddleDiscountId,
				userId: user.id,
				email: user.email ?? undefined,
			});

			if (!opened) {
				appToast.error("Could not open checkout");
				setCheckingOut(null);
			}
		},
		[user],
	);

	// Paddle provisions the subscription through the webhook, a few seconds
	// after checkout closes. Poll our own endpoint until the entitlement flips.
	useEffect(() => {
		if (!awaitingActivation) return;
		let tries = 0;
		const interval = setInterval(async () => {
			tries += 1;
			const next = await refresh();
			if (next?.isPro) {
				clearInterval(interval);
				setAwaitingActivation(false);
				setCheckingOut(null);
				appToast.success("You're on Pro. Welcome aboard.");
			} else if (tries >= 20) {
				clearInterval(interval);
				setAwaitingActivation(false);
				setCheckingOut(null);
				appToast.info("Still waiting on Paddle", "Your payment went through. Pro switches on as soon as Paddle confirms it; refresh in a minute.");
			}
		}, 3000);
		return () => clearInterval(interval);
	}, [awaitingActivation, refresh]);

	// Arriving from a pricing CTA or an upgrade prompt opens checkout directly.
	useEffect(() => {
		if (!user?.id || loading || isPro) return;
		const requested = new URLSearchParams(window.location.search).get("checkout");
		if (!requested) return;
		const { monthly, yearly } = pickPlans(plans);
		const plan = requested === "pro-yearly" ? (yearly ?? monthly) : (monthly ?? yearly);
		if (!plan) return;
		// After this render, so the overlay opens over a settled page. The URL
		// is cleared in the same tick, so a cancelled run leaves it for the next.
		const timer = setTimeout(() => {
			window.history.replaceState({}, "", "/billing");
			void startCheckout(plan);
		}, 0);
		return () => clearTimeout(timer);
	}, [user, loading, isPro, plans, startCheckout]);

	return (
		<div className="mx-auto max-w-4xl space-y-8">
			<header>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">Billing</h1>
				<p className="mt-1 text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Your Mizan plan. Payments are handled by Paddle, our merchant of record.
				</p>
			</header>

			{awaitingActivation && (
				<div role="status" className="flex items-center gap-3 rounded-2xl border border-brand-500/30 bg-brand-50 p-4 text-sm text-brand-800 dark:bg-brand-950 dark:text-brand-200">
					<Loading size="sm" />
					Switching on Pro. This usually takes a few seconds.
				</div>
			)}

			{loading && !subscription ? (
				<div className="flex justify-center py-16"><Loading /></div>
			) : isPro && subscription ? (
				<Subscribed subscription={subscription} plans={plans} onChanged={refresh} />
			) : (
				<ChoosePlan plans={plans} checkingOut={checkingOut} onChoose={startCheckout} />
			)}

			{subscription?.hasBillingAccount && <History />}

			<p className="text-center text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				Prices in USD, taxes added where they apply. 14-day refund on your first subscription.{" "}
				<a href="https://zaftech.co/refunds" target="_blank" rel="noopener noreferrer" className="footer-link underline">Refund policy</a>
				{" · "}
				<a href="https://zaftech.co/terms" target="_blank" rel="noopener noreferrer" className="footer-link underline">Terms</a>
			</p>
		</div>
	);
}

function ChoosePlan({
	plans,
	checkingOut,
	onChoose,
}: {
	plans: BillingPlan[];
	checkingOut: string | null;
	onChoose: (plan: BillingPlan) => void;
}) {
	const { monthly, yearly } = pickPlans(plans);
	const [interval, setBillingInterval] = useState<"month" | "year">(monthly ? "month" : "year");
	const plan = interval === "year" ? (yearly ?? monthly) : (monthly ?? yearly);
	const saving = yearlySavingPercent(monthly, yearly);

	if (!plan) {
		return (
			<div className="card mx-auto max-w-sm p-6 text-center text-sm text-charcoal-blue-600 dark:text-charcoal-blue-400 sm:p-8">
				Pro is not on sale right now. Check back soon.
			</div>
		);
	}

	const deal = plan.deal;
	return (
		<div className="card mx-auto max-w-sm p-6 sm:p-8">
			{monthly && yearly && (
				<div role="radiogroup" aria-label="Billing period" className="mb-5 grid grid-cols-2 border border-charcoal-blue-200 p-0.5 text-sm dark:border-charcoal-blue-700">
					{(["month", "year"] as const).map((value) => (
						<button
							key={value}
							type="button"
							role="radio"
							aria-checked={interval === value}
							onClick={() => setBillingInterval(value)}
							className={`px-3 py-1.5 font-medium transition-colors ${
								interval === value
									? "bg-charcoal-blue-900 text-white dark:bg-charcoal-blue-50 dark:text-charcoal-blue-950"
									: "text-charcoal-blue-600 hover:text-charcoal-blue-900 dark:text-charcoal-blue-400 dark:hover:text-charcoal-blue-100"
							}`}
						>
							{value === "month" ? "Monthly" : `Yearly${saving ? ` · save ${saving}%` : ""}`}
						</button>
					))}
				</div>
			)}

			<h2 className="text-lg font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">{plan.name}</h2>
			<div className="mt-2 flex flex-wrap items-baseline gap-2">
				{deal ? (
					<>
						<span className="num text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
							{formatMoney(deal.discountedAmountCents, plan.currency)}
						</span>
						<span className="num text-base text-charcoal-blue-400 line-through dark:text-charcoal-blue-500">
							{formatMoney(plan.amountCents, plan.currency)}
						</span>
					</>
				) : (
					<span className="num text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
						{formatMoney(plan.amountCents, plan.currency)}
					</span>
				)}
				<span className="text-xs uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">{cadence(plan.interval)}</span>
			</div>
			{deal && (
				<p className="mt-2 text-sm font-medium text-verdigris-700 dark:text-verdigris-400">
					{deal.label}: {describeDeal(deal, plan.interval)}
					{deal.expiresAt ? `, until ${formatDate(deal.expiresAt)}` : ""}
				</p>
			)}
			<p className="mt-3 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-400">
				{plan.trialDays ? `${plan.trialDays}-day free trial. ` : ""}Cancel anytime from this page.
			</p>
			<ul className="mt-4 space-y-2">
				{PRO_PERKS.map((perk) => (
					<li key={perk} className="flex items-start gap-2 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
						<Icon name="circleCheck" size={14} className="mt-0.5 shrink-0 text-verdigris-700 dark:text-verdigris-400" aria-hidden="true" />
						{perk}
					</li>
				))}
			</ul>
			<button type="button" disabled={checkingOut !== null} onClick={() => onChoose(plan)} className="btn-primary mt-6 w-full">
				{checkingOut === plan.id ? <Loading size="sm" /> : "Go Pro"}
			</button>
			<p className="mt-4 text-center text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				Rather run it yourself? <a href="https://github.com/ZafTec/mizan#self-hosting" className="footer-link underline">Read the setup guide</a>.
			</p>
		</div>
	);
}

function Subscribed({
	subscription,
	plans,
	onChanged,
}: {
	subscription: MySubscription;
	plans: BillingPlan[];
	onChanged: () => Promise<unknown>;
}) {
	const [busy, setBusy] = useState<"portal" | "cancel" | "resume" | "switch" | null>(null);
	const [confirmCancel, setConfirmCancel] = useState(false);
	const [preview, setPreview] = useState<{ plan: BillingPlan; preview: PlanChangePreview } | null>(null);

	const periodEnd = formatDate(subscription.currentPeriodEnd);
	const nextBilled = formatDate(subscription.nextBilledAt);
	const cancelsAt = formatDate(subscription.cancelsAt);
	const trialEnd = formatDate(subscription.trialEndsAt);
	const switchTo = plans.find((p) => p.id !== subscription.planId && p.interval !== subscription.interval);

	async function run<T>(action: typeof busy, work: () => Promise<T>, failure: string): Promise<T | null> {
		setBusy(action);
		try {
			return await work();
		} catch (error) {
			appToast.error(firstErrorMessage(error, failure));
			return null;
		} finally {
			setBusy(null);
		}
	}

	async function openPaymentPortal() {
		const portal = await run("portal", getBillingPortal, "Could not reach Paddle. Try again in a moment.");
		if (!portal) return;
		const url = portal.updatePaymentMethodUrl ?? portal.overviewUrl;
		window.open(url, "_blank", "noopener,noreferrer");
	}

	async function cancel() {
		const result = await run("cancel", () => clientApi<MySubscription>("/api/Subscriptions/cancel", { method: "POST" }), "Could not cancel");
		setConfirmCancel(false);
		if (result) {
			await onChanged();
			appToast.success("Subscription canceled", result.cancelsAt ? `You keep Pro until ${formatDate(result.cancelsAt)}.` : undefined);
		}
	}

	async function resume() {
		const result = await run("resume", () => clientApi<MySubscription>("/api/Subscriptions/resume", { method: "POST" }), "Could not keep the subscription");
		if (result) {
			await onChanged();
			appToast.success("Your subscription continues");
		}
	}

	async function showSwitch(plan: BillingPlan) {
		const result = await run(
			"switch",
			() => clientApi<PlanChangePreview>("/api/Subscriptions/change-plan/preview", { method: "POST", body: { planId: plan.id } }),
			"Could not price the change",
		);
		if (result) setPreview({ plan, preview: result });
	}

	async function confirmSwitch() {
		if (!preview) return;
		const result = await run(
			"switch",
			() => clientApi<MySubscription>("/api/Subscriptions/change-plan", { method: "POST", body: { planId: preview.plan.id } }),
			"Could not switch plans",
		);
		if (result) {
			setPreview(null);
			await onChanged();
			appToast.success(`You're on ${preview.plan.name}`);
		}
	}

	if (subscription.isLifetime) {
		return (
			<div className="card p-6 sm:p-8">
				<h2 className="text-xl font-medium tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-2xl">Lifetime Pro, forever</h2>
				<p className="mt-1 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-400">
					Thanks for being a founding member. Every feature we ship next is already yours.
				</p>
			</div>
		);
	}

	return (
		<div className="card p-6 sm:p-8">
			<div className="flex flex-wrap items-start justify-between gap-4">
				<div>
					<div className="flex flex-wrap items-center gap-2">
						<h2 className="text-xl font-medium tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-2xl">
							{subscription.planName ?? "Pro"}
						</h2>
						<span className="inline-flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.1em] text-verdigris-700 dark:text-verdigris-400">
							<span className="h-1.5 w-1.5 rounded-full bg-current" aria-hidden="true" />
							{subscription.status.replace("_", " ")}
						</span>
					</div>
					{subscription.amountCents != null && (
						<p className="num mt-1 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-400">
							{formatMoney(subscription.amountCents, subscription.currency ?? "USD")}
							{cadence(subscription.interval)}
						</p>
					)}
				</div>
			</div>

			<div className="mt-4 space-y-1 text-sm">
				{cancelsAt ? (
					<p className="text-tuscan-sun-700 dark:text-tuscan-sun-400">Ends {cancelsAt}. You keep Pro until then, and nothing more is charged.</p>
				) : subscription.status === "trialing" && trialEnd ? (
					<p className="text-charcoal-blue-700 dark:text-charcoal-blue-300">Free trial until {trialEnd}; the first payment is taken then.</p>
				) : nextBilled ? (
					<p className="text-charcoal-blue-700 dark:text-charcoal-blue-300">Renews {nextBilled}.</p>
				) : periodEnd ? (
					<p className="text-charcoal-blue-700 dark:text-charcoal-blue-300">Paid through {periodEnd}.</p>
				) : null}
				{subscription.status === "past_due" && (
					<p className="text-red-700 dark:text-red-400">The last payment did not go through. Update your payment method to keep Pro.</p>
				)}
			</div>

			<ul className="mt-4 grid grid-cols-1 gap-2 sm:grid-cols-2">
				{PRO_PERKS.map((perk) => (
					<li key={perk} className="flex items-start gap-2 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
						<Icon name="circleCheck" size={14} className="mt-0.5 shrink-0 text-verdigris-700 dark:text-verdigris-400" aria-hidden="true" />
						{perk}
					</li>
				))}
			</ul>

			{subscription.canManage && (
				<div className="mt-6 flex flex-wrap items-center gap-2 border-t border-charcoal-blue-200 pt-5 dark:border-charcoal-blue-700">
					{cancelsAt ? (
						<button type="button" onClick={resume} disabled={busy !== null} className="btn-primary btn-sm">
							{busy === "resume" ? <Loading size="sm" /> : "Keep my subscription"}
						</button>
					) : (
						switchTo && (
							<button type="button" onClick={() => showSwitch(switchTo)} disabled={busy !== null} className="btn-secondary btn-sm">
								{busy === "switch" && !preview ? <Loading size="sm" /> : `Switch to ${switchTo.interval === "year" ? "yearly" : "monthly"}`}
							</button>
						)
					)}
					<button type="button" onClick={openPaymentPortal} disabled={busy !== null} className="btn-secondary btn-sm">
						{busy === "portal" ? <Loading size="sm" /> : "Update payment method"}
					</button>
					{!cancelsAt && (
						<button type="button" onClick={() => setConfirmCancel(true)} disabled={busy !== null} className="btn-ghost btn-sm">
							Cancel subscription
						</button>
					)}
				</div>
			)}

			<ConfirmationModal
				isOpen={confirmCancel}
				onClose={() => setConfirmCancel(false)}
				onConfirm={cancel}
				title="Cancel Pro?"
				message={`Nothing more is charged. You keep Pro until ${nextBilled ?? periodEnd ?? "the end of the period you paid for"}, then the account returns to Free. Everything you have logged stays.`}
				confirmText="Cancel subscription"
				isLoading={busy === "cancel"}
				isDanger
			/>

			<ModalShell open={preview !== null} onClose={() => setPreview(null)}>
				{preview && (
					<div className="border border-charcoal-blue-200 bg-white p-6 dark:border-white/10 dark:bg-charcoal-blue-950 sm:p-8">
						<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Switch to {preview.plan.name}?</h2>
						<dl className="mt-4 space-y-2 text-sm">
							<div className="flex justify-between gap-4">
								<dt className="text-charcoal-blue-600 dark:text-charcoal-blue-400">
									{preview.preview.dueNowCents < 0 ? "Credit toward later bills" : "Charged now"}
								</dt>
								<dd className="num font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
									{formatMoney(Math.abs(preview.preview.dueNowCents), preview.preview.currency)}
								</dd>
							</div>
							{preview.preview.nextAmountCents != null && (
								<div className="flex justify-between gap-4">
									<dt className="text-charcoal-blue-600 dark:text-charcoal-blue-400">
										Then{preview.preview.nextBilledAt ? ` from ${formatDate(preview.preview.nextBilledAt)}` : ""}
									</dt>
									<dd className="num font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
										{formatMoney(preview.preview.nextAmountCents, preview.preview.currency)}
										{cadence(preview.plan.interval)}
									</dd>
								</div>
							)}
						</dl>
						<p className="mt-3 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
							The time left on your current plan is credited. Taxes are included where they apply.
						</p>
						<div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
							<button type="button" onClick={() => setPreview(null)} className="btn-ghost">Not now</button>
							<button type="button" onClick={confirmSwitch} disabled={busy !== null} className="btn-primary">
								{busy === "switch" ? <Loading size="sm" /> : "Confirm switch"}
							</button>
						</div>
					</div>
				)}
			</ModalShell>
		</div>
	);
}

function History() {
	const [transactions, setTransactions] = useState<BillingTransaction[] | null>(null);
	const [opening, setOpening] = useState<string | null>(null);

	useEffect(() => {
		let cancelled = false;
		clientApi<BillingTransaction[]>("/api/Subscriptions/transactions")
			.then((items) => !cancelled && setTransactions(items))
			.catch(() => !cancelled && setTransactions([]));
		return () => {
			cancelled = true;
		};
	}, []);

	async function download(id: string) {
		setOpening(id);
		try {
			const { url } = await clientApi<{ url: string }>(`/api/Subscriptions/transactions/${encodeURIComponent(id)}/invoice`);
			window.open(url, "_blank", "noopener,noreferrer");
		} catch (error) {
			appToast.error(firstErrorMessage(error, "That invoice is not ready yet."));
		} finally {
			setOpening(null);
		}
	}

	return (
		<section aria-labelledby="history-heading" className="card p-6 sm:p-8">
			<h2 id="history-heading" className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Payments</h2>
			{transactions === null ? (
				<div className="flex justify-center py-6"><Loading size="sm" /></div>
			) : transactions.length === 0 ? (
				<p className="mt-2 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">No payments yet.</p>
			) : (
				<ul className="mt-3 divide-y divide-charcoal-blue-200 dark:divide-charcoal-blue-700">
					{transactions.map((t) => (
						<li key={t.id} className="flex flex-wrap items-center justify-between gap-3 py-3 text-sm">
							<div>
								<p className="num font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">{formatMoney(t.totalCents, t.currency)}</p>
								<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
									{formatDate(t.billedAt) ?? "Pending"}
									{t.status === "past_due" ? " · payment failed" : ""}
								</p>
							</div>
							{t.invoiceNumber && (
								<button type="button" onClick={() => download(t.id)} disabled={opening !== null} className="btn-ghost btn-sm">
									{opening === t.id ? <Loading size="sm" /> : `Invoice ${t.invoiceNumber}`}
								</button>
							)}
						</li>
					))}
				</ul>
			)}
		</section>
	);
}
