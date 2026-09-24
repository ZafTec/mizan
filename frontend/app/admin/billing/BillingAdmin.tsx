"use client";

import { useState, useTransition, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import { Pill } from "@/components/ui/data-table";
import ConfirmationModal from "@/components/ConfirmationModal";
import { ModalShell } from "@/components/ModalShell";
import {
	describeDeal,
	firstErrorMessage,
	formatMoney,
	type AdminBillingCatalog,
	type AdminBillingDiscount,
	type AdminBillingPlan,
} from "@/lib/billing";

/** "2.99" -> 299. Empty or unparseable input is NaN, which the API rejects with a message. */
function toCents(dollars: string): number {
	return Math.round(Number.parseFloat(dollars) * 100);
}

function Field({ label, helper, children }: { label: string; helper?: string; children: ReactNode }) {
	return (
		<label className="block space-y-1.5">
			<span className="text-sm font-medium text-charcoal-blue-800 dark:text-charcoal-blue-200">{label}</span>
			{children}
			{helper && <span className="block text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">{helper}</span>}
		</label>
	);
}

/**
 * The Pro catalogue - docs/ARCHITECTURE.md#billing. Plans are Paddle prices on
 * the Mizan Pro product; a price never changes in place, so "Change price"
 * archives the old one (its subscribers keep it) and puts a new one on sale.
 * Deals are Paddle discounts: without a code, checkout applies them and the
 * pricing page advertises them.
 */
export default function BillingAdmin({ catalog }: { catalog: AdminBillingCatalog }) {
	const router = useRouter();
	const [pending, startTransition] = useTransition();
	const [archivePlan, setArchivePlan] = useState<AdminBillingPlan | null>(null);
	const [archiveDiscount, setArchiveDiscount] = useState<AdminBillingDiscount | null>(null);
	const [repricing, setRepricing] = useState<AdminBillingPlan | null>(null);
	const [editing, setEditing] = useState<AdminBillingPlan | null>(null);

	const activePlans = catalog.plans.filter((p) => p.isActive);
	const planName = (id: string) => catalog.plans.find((p) => p.id === id)?.name ?? "Archived plan";

	function mutate(work: () => Promise<unknown>, success: string, done?: () => void) {
		startTransition(async () => {
			try {
				await work();
				appToast.success(success);
				done?.();
				router.refresh();
			} catch (error) {
				appToast.error(firstErrorMessage(error, "Paddle did not accept the change"));
			}
		});
	}

	return (
		<div className="space-y-8">
			<div className="flex flex-wrap items-center gap-3 text-sm">
				<Pill tone={catalog.environment === "production" ? "good" : "warn"}>Paddle {catalog.environment}</Pill>
				{!catalog.configured && (
					<span className="text-red-700 dark:text-red-400">No Paddle API key is set on the server; changes will fail.</span>
				)}
			</div>

			<section className="card space-y-4 p-6" aria-labelledby="plans-heading">
				<div className="flex flex-wrap items-center justify-between gap-3">
					<h2 id="plans-heading" className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Plans</h2>
					<button
						type="button"
						className="btn-secondary btn-sm"
						disabled={pending}
						onClick={() =>
							mutate(
								async () => {
									const { imported } = await clientApi<{ imported: number }>("/api/admin/billing/plans/import", { method: "POST" });
									if (imported === 0) throw new Error("Every Pro price in Paddle is already listed.");
								},
								"Imported the Pro prices from Paddle",
							)
						}
					>
						Import from Paddle
					</button>
				</div>

				{catalog.plans.length === 0 ? (
					<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Nothing is on sale. Create a plan below, or import the prices already in Paddle.
					</p>
				) : (
					<div className="-mx-6 overflow-x-auto">
						<table className="w-full min-w-[640px] text-sm">
							<thead className="text-left text-xs uppercase tracking-wide text-charcoal-blue-500 dark:text-charcoal-blue-400">
								<tr>
									<th className="px-6 py-2 font-medium">Plan</th>
									<th className="px-3 py-2 font-medium">Price</th>
									<th className="px-3 py-2 font-medium">Trial</th>
									<th className="px-3 py-2 font-medium">Subscribers</th>
									<th className="px-3 py-2 font-medium">Status</th>
									<th className="px-6 py-2 text-right font-medium">Actions</th>
								</tr>
							</thead>
							<tbody className="divide-y divide-charcoal-blue-200 dark:divide-charcoal-blue-700">
								{catalog.plans.map((plan) => (
									<tr key={plan.id}>
										<td className="px-6 py-3">
											<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">{plan.name}</p>
											<p className="font-mono text-[11px] text-charcoal-blue-400">{plan.paddlePriceId}</p>
										</td>
										<td className="num px-3 py-3">
											{formatMoney(plan.amountCents, plan.currency)}
											{plan.interval === "year" ? "/yr" : "/mo"}
										</td>
										<td className="px-3 py-3">{plan.trialDays ? `${plan.trialDays} days` : "—"}</td>
										<td className="num px-3 py-3">{plan.subscribers}</td>
										<td className="px-3 py-3">
											{plan.isActive ? <Pill tone="good">On sale</Pill> : <Pill>Archived</Pill>}
										</td>
										<td className="px-6 py-3 text-right">
											{plan.isActive && (
												<div className="flex justify-end gap-1">
													<button type="button" className="btn-ghost btn-sm" onClick={() => setEditing(plan)}>Edit</button>
													<button type="button" className="btn-ghost btn-sm" onClick={() => setRepricing(plan)}>Change price</button>
													<button type="button" className="btn-ghost btn-sm text-red-700 dark:text-red-400" onClick={() => setArchivePlan(plan)}>
														Archive
													</button>
												</div>
											)}
										</td>
									</tr>
								))}
							</tbody>
						</table>
					</div>
				)}
			</section>

			<CreatePlan
				pending={pending}
				onCreate={(body) => mutate(() => clientApi("/api/admin/billing/plans", { method: "POST", body }), `Created ${body.name} in Paddle`)}
			/>

			<section className="card space-y-4 p-6" aria-labelledby="discounts-heading">
				<h2 id="discounts-heading" className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Discounts and deals</h2>
				{catalog.discounts.length === 0 ? (
					<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">No discounts yet.</p>
				) : (
					<ul className="divide-y divide-charcoal-blue-200 dark:divide-charcoal-blue-700">
						{catalog.discounts.map((d) => (
							<li key={d.id} className="flex flex-wrap items-center justify-between gap-3 py-3 text-sm">
								<div className="min-w-0 space-y-0.5">
									<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
										{d.label}{" "}
										{d.code ? <Pill tone="info">Code {d.code}</Pill> : <Pill tone="good">Automatic</Pill>}{" "}
										{!d.isActive && <Pill>Archived</Pill>}
									</p>
									<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
										{describeDeal(d)}
										{" · "}
										{d.planIds.length === 0 ? "every plan" : d.planIds.map(planName).join(", ")}
										{d.expiresAt ? ` · until ${new Date(d.expiresAt).toLocaleDateString()}` : ""}
									</p>
								</div>
								{d.isActive && (
									<button type="button" className="btn-ghost btn-sm text-red-700 dark:text-red-400" onClick={() => setArchiveDiscount(d)}>
										Archive
									</button>
								)}
							</li>
						))}
					</ul>
				)}
			</section>

			<CreateDiscount
				plans={activePlans}
				pending={pending}
				onCreate={(body) => mutate(() => clientApi("/api/admin/billing/discounts", { method: "POST", body }), `Created ${body.label} in Paddle`)}
			/>

			<ConfirmationModal
				isOpen={archivePlan !== null}
				onClose={() => setArchivePlan(null)}
				onConfirm={() =>
					archivePlan &&
					mutate(() => clientApi(`/api/admin/billing/plans/${archivePlan.id}/archive`, { method: "POST" }), `${archivePlan.name} is off sale`, () => setArchivePlan(null))
				}
				title={`Take ${archivePlan?.name ?? "this plan"} off sale?`}
				message={`New customers can no longer choose it. The ${archivePlan?.subscribers ?? 0} subscriber(s) on it keep renewing at their price.`}
				confirmText="Archive plan"
				isLoading={pending}
				isDanger
			/>

			<ConfirmationModal
				isOpen={archiveDiscount !== null}
				onClose={() => setArchiveDiscount(null)}
				onConfirm={() =>
					archiveDiscount &&
					mutate(() => clientApi(`/api/admin/billing/discounts/${archiveDiscount.id}/archive`, { method: "POST" }), `${archiveDiscount.label} is archived`, () => setArchiveDiscount(null))
				}
				title={`Archive ${archiveDiscount?.label ?? "this discount"}?`}
				message="Checkout stops applying it. Subscriptions that already have it keep it for the periods they were promised."
				confirmText="Archive discount"
				isLoading={pending}
				isDanger
			/>

			<ModalShell open={repricing !== null} onClose={() => setRepricing(null)}>
				{repricing && (
					<Reprice
						plan={repricing}
						pending={pending}
						onCancel={() => setRepricing(null)}
						onSave={(amountCents, trialDays) =>
							mutate(
								() => clientApi(`/api/admin/billing/plans/${repricing.id}/price`, { method: "POST", body: { id: repricing.id, amountCents, trialDays } }),
								`${repricing.name} now costs ${formatMoney(amountCents)}`,
								() => setRepricing(null),
							)
						}
					/>
				)}
			</ModalShell>

			<ModalShell open={editing !== null} onClose={() => setEditing(null)}>
				{editing && (
					<EditPlan
						plan={editing}
						pending={pending}
						onCancel={() => setEditing(null)}
						onSave={(body) =>
							mutate(() => clientApi(`/api/admin/billing/plans/${editing.id}`, { method: "PUT", body: { id: editing.id, ...body } }), "Plan updated", () => setEditing(null))
						}
					/>
				)}
			</ModalShell>
		</div>
	);
}

interface NewPlan {
	name: string;
	description: string | null;
	interval: "month" | "year";
	amountCents: number;
	trialDays: number | null;
	sortOrder: number;
}

function CreatePlan({ pending, onCreate }: { pending: boolean; onCreate: (plan: NewPlan) => void }) {
	const [name, setName] = useState("Pro Monthly");
	const [interval, setIntervalValue] = useState<"month" | "year">("month");
	const [price, setPrice] = useState("2.99");
	const [trial, setTrial] = useState("");
	const [sortOrder, setSortOrder] = useState("0");

	return (
		<form
			className="card space-y-4 p-6"
			onSubmit={(e) => {
				e.preventDefault();
				onCreate({
					name: name.trim(),
					description: null,
					interval,
					amountCents: toCents(price),
					trialDays: trial ? Number(trial) : null,
					sortOrder: Number(sortOrder) || 0,
				});
			}}
		>
			<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">New plan</h2>
			<div className="grid gap-4 sm:grid-cols-2">
				<Field label="Name" helper="Shown at checkout and on invoices.">
					<input className="input w-full" value={name} onChange={(e) => setName(e.target.value)} maxLength={100} required />
				</Field>
				<Field label="Billed">
					<select className="input w-full" value={interval} onChange={(e) => setIntervalValue(e.target.value as "month" | "year")}>
						<option value="month">Monthly</option>
						<option value="year">Yearly</option>
					</select>
				</Field>
				<Field label="Price (USD)">
					<input className="input w-full tabular-nums" inputMode="decimal" value={price} onChange={(e) => setPrice(e.target.value)} required />
				</Field>
				<Field label="Free trial (days)" helper="Blank for none. A card is still required.">
					<input className="input w-full tabular-nums" type="number" min={0} max={90} value={trial} onChange={(e) => setTrial(e.target.value)} />
				</Field>
				<Field label="Order" helper="Lower shows first.">
					<input className="input w-full tabular-nums" type="number" min={0} max={1000} value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
				</Field>
			</div>
			<button type="submit" className="btn-primary" disabled={pending}>Create in Paddle</button>
		</form>
	);
}

interface NewDiscount {
	label: string;
	code: string | null;
	type: "percentage" | "flat";
	amount: number;
	recurring: boolean;
	maximumRecurringIntervals: number | null;
	expiresAt: string | null;
	planIds: string[];
}

function CreateDiscount({
	plans,
	pending,
	onCreate,
}: {
	plans: AdminBillingPlan[];
	pending: boolean;
	onCreate: (discount: NewDiscount) => void;
}) {
	const [label, setLabel] = useState("");
	const [code, setCode] = useState("");
	const [type, setType] = useState<"percentage" | "flat">("percentage");
	const [amount, setAmount] = useState("20");
	const [recurring, setRecurring] = useState(false);
	const [periods, setPeriods] = useState("");
	const [expires, setExpires] = useState("");
	const [planIds, setPlanIds] = useState<string[]>([]);

	return (
		<form
			className="card space-y-4 p-6"
			onSubmit={(e) => {
				e.preventDefault();
				onCreate({
					label: label.trim(),
					code: code.trim() || null,
					type,
					amount: type === "flat" ? toCents(amount) : Number(amount),
					recurring,
					maximumRecurringIntervals: recurring && periods ? Number(periods) : null,
					expiresAt: expires ? new Date(`${expires}T23:59:59`).toISOString() : null,
					planIds,
				});
			}}
		>
			<div>
				<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">New discount</h2>
				<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Leave the code blank for a deal: checkout applies it and the pricing page shows it. A yearly deal is a discount limited to the yearly plan.
				</p>
			</div>
			<div className="grid gap-4 sm:grid-cols-2">
				<Field label="Label" helper='Shown on the pricing page, e.g. "Launch offer".'>
					<input className="input w-full" value={label} onChange={(e) => setLabel(e.target.value)} maxLength={100} required />
				</Field>
				<Field label="Code" helper="Optional. 3 to 16 letters or digits.">
					<input className="input w-full uppercase" value={code} onChange={(e) => setCode(e.target.value)} maxLength={16} />
				</Field>
				<Field label="Type">
					<select className="input w-full" value={type} onChange={(e) => setType(e.target.value as "percentage" | "flat")}>
						<option value="percentage">Percentage off</option>
						<option value="flat">Amount off (USD)</option>
					</select>
				</Field>
				<Field label={type === "percentage" ? "Percent" : "Amount (USD)"}>
					<input className="input w-full tabular-nums" inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} required />
				</Field>
				<Field label="Expires" helper="Blank for no end date.">
					<input className="input w-full" type="date" value={expires} onChange={(e) => setExpires(e.target.value)} />
				</Field>
				<div className="space-y-2">
					<label className="flex items-center gap-2 text-sm text-charcoal-blue-800 dark:text-charcoal-blue-200">
						<input type="checkbox" checked={recurring} onChange={(e) => setRecurring(e.target.checked)} />
						Applies to renewals too
					</label>
					{recurring && (
						<Field label="For how many billing periods" helper="Blank for as long as the subscription runs.">
							<input className="input w-full tabular-nums" type="number" min={1} max={120} value={periods} onChange={(e) => setPeriods(e.target.value)} />
						</Field>
					)}
				</div>
			</div>
			{plans.length > 0 && (
				<fieldset className="space-y-2">
					<legend className="text-sm font-medium text-charcoal-blue-800 dark:text-charcoal-blue-200">Applies to (none checked means every plan)</legend>
					<div className="flex flex-wrap gap-4">
						{plans.map((plan) => (
							<label key={plan.id} className="flex items-center gap-2 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
								<input
									type="checkbox"
									checked={planIds.includes(plan.id)}
									onChange={(e) => setPlanIds((ids) => (e.target.checked ? [...ids, plan.id] : ids.filter((id) => id !== plan.id)))}
								/>
								{plan.name}
							</label>
						))}
					</div>
				</fieldset>
			)}
			<button type="submit" className="btn-primary" disabled={pending}>Create in Paddle</button>
		</form>
	);
}

function Reprice({
	plan,
	pending,
	onCancel,
	onSave,
}: {
	plan: AdminBillingPlan;
	pending: boolean;
	onCancel: () => void;
	onSave: (amountCents: number, trialDays: number | null) => void;
}) {
	const [price, setPrice] = useState((plan.amountCents / 100).toFixed(2));
	const [trial, setTrial] = useState(plan.trialDays ? String(plan.trialDays) : "");

	return (
		<form
			className="space-y-4 border border-charcoal-blue-200 bg-white p-6 dark:border-white/10 dark:bg-charcoal-blue-950 sm:p-8"
			onSubmit={(e) => {
				e.preventDefault();
				onSave(toCents(price), trial ? Number(trial) : null);
			}}
		>
			<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">New price for {plan.name}</h2>
			<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
				Creates a new price in Paddle and archives the current one. The {plan.subscribers} existing subscriber(s) keep paying{" "}
				{formatMoney(plan.amountCents, plan.currency)}; deals limited to this plan carry over.
			</p>
			<Field label="Price (USD)">
				<input className="input w-full tabular-nums" inputMode="decimal" value={price} onChange={(e) => setPrice(e.target.value)} required />
			</Field>
			<Field label="Free trial (days)">
				<input className="input w-full tabular-nums" type="number" min={0} max={90} value={trial} onChange={(e) => setTrial(e.target.value)} />
			</Field>
			<div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
				<button type="button" className="btn-ghost" onClick={onCancel}>Cancel</button>
				<button type="submit" className="btn-primary" disabled={pending}>Change price</button>
			</div>
		</form>
	);
}

function EditPlan({
	plan,
	pending,
	onCancel,
	onSave,
}: {
	plan: AdminBillingPlan;
	pending: boolean;
	onCancel: () => void;
	onSave: (body: { name: string; description: string | null; sortOrder: number }) => void;
}) {
	const [name, setName] = useState(plan.name);
	const [description, setDescription] = useState(plan.description ?? "");
	const [sortOrder, setSortOrder] = useState(String(plan.sortOrder));

	return (
		<form
			className="space-y-4 border border-charcoal-blue-200 bg-white p-6 dark:border-white/10 dark:bg-charcoal-blue-950 sm:p-8"
			onSubmit={(e) => {
				e.preventDefault();
				onSave({ name: name.trim(), description: description.trim() || null, sortOrder: Number(sortOrder) || 0 });
			}}
		>
			<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Edit {plan.name}</h2>
			<Field label="Name" helper="Mizan's label. Paddle keeps the name the price was created with.">
				<input className="input w-full" value={name} onChange={(e) => setName(e.target.value)} maxLength={100} required />
			</Field>
			<Field label="Description">
				<textarea className="input w-full" rows={2} value={description} onChange={(e) => setDescription(e.target.value)} maxLength={500} />
			</Field>
			<Field label="Order">
				<input className="input w-full tabular-nums" type="number" min={0} max={1000} value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
			</Field>
			<div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
				<button type="button" className="btn-ghost" onClick={onCancel}>Cancel</button>
				<button type="submit" className="btn-primary" disabled={pending}>Save</button>
			</div>
		</form>
	);
}
