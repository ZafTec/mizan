"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { clientApi } from "@/lib/api.client";

interface ShoppingListItem {
	id: string;
	ingredientText: string;
	quantity: number;
	unit: string;
	isChecked: boolean;
}

interface ShoppingList {
	id: string;
	name: string;
	createdAt: string;
	items: ShoppingListItem[];
}

const amount = (item: ShoppingListItem) =>
	item.quantity && item.unit ? `${Number(item.quantity).toLocaleString()} ${item.unit}` : item.quantity ? String(item.quantity) : "";

export default function ShoppingListPage() {
	const [lists, setLists] = useState<ShoppingList[] | null>(null);
	const [failed, setFailed] = useState(false);
	const [showBought, setShowBought] = useState(false);
	const [attempt, setAttempt] = useState(0);

	useEffect(() => {
		let cancelled = false;
		clientApi<{ items: ShoppingList[] }>("/api/ShoppingLists")
			.then((data) => {
				if (!cancelled) setLists(data.items || []);
			})
			.catch(() => {
				if (!cancelled) {
					setFailed(true);
					setLists([]);
				}
			});
		return () => {
			cancelled = true;
		};
	}, [attempt]);

	const retry = () => {
		setFailed(false);
		setLists(null);
		setAttempt((n) => n + 1);
	};

	const toggle = async (item: ShoppingListItem) => {
		const next = !item.isChecked;
		// Optimistic: the tick is instant and reverts only if the server disagrees.
		const apply = (checked: boolean) =>
			setLists((current) =>
				(current ?? []).map((list) => ({
					...list,
					items: list.items.map((candidate) => (candidate.id === item.id ? { ...candidate, isChecked: checked } : candidate)),
				})),
			);
		apply(next);
		try {
			await clientApi(`/api/ShoppingLists/items/${item.id}/toggle`, { method: "PATCH", body: { IsChecked: next } });
		} catch {
			apply(!next);
		}
	};

	const items = (lists ?? []).flatMap((list) => list.items);
	const left = items.filter((item) => !item.isChecked);
	const shown = showBought ? items : left;

	return (
		<div className="log-page space-y-8" data-testid="shopping-list-page">
			<header className="log-page-header block!">
				<h1>Shopping list</h1>
				<p className="log-muted mt-2">What your plans need you to buy.</p>
			</header>

			<section aria-labelledby="buy-title">
				<header className="log-section-header">
					<h2 id="buy-title">To buy</h2>
					{items.length > 0 && (
						<span className="num text-sm log-muted">
							{left.length} left of {items.length}
						</span>
					)}
				</header>

				{lists === null ? (
					<ul aria-busy="true" aria-label="Loading your list">
						{[0, 1, 2].map((row) => (
							<li key={row} className="ledger-row">
								<span className="h-5 w-5 rounded-sm bg-muted" />
								<span className="h-3 flex-1 rounded-sm bg-muted" style={{ maxWidth: `${40 + row * 12}%` }} />
							</li>
						))}
					</ul>
				) : failed ? (
					<div className="log-empty">
						<p>Your list could not be loaded.</p>
						<button type="button" onClick={retry} className="text-link mt-3 text-sm">
							Try again
						</button>
					</div>
				) : items.length === 0 ? (
					<div className="log-empty">
						<p>Nothing to buy.</p>
						<p className="log-muted mt-2 text-sm">Add recipes to a meal plan and their ingredients land here.</p>
						<Link href="/meal-plan" className="text-link mt-3 inline-block text-sm">
							Open meal plans →
						</Link>
					</div>
				) : (
					<>
						{shown.length === 0 && (
							<p className="log-empty">Everything is bought.</p>
						)}
						<ul>
							{shown.map((item) => (
								<li key={item.id}>
									<label className="ledger-row cursor-pointer">
										<input
											type="checkbox"
											checked={item.isChecked}
											onChange={() => void toggle(item)}
											className="h-5 w-5 shrink-0 accent-brand-700"
										/>
										<span className={`min-w-0 flex-1 ${item.isChecked ? "log-muted line-through" : ""}`}>{item.ingredientText}</span>
										<span className={`num whitespace-nowrap text-sm ${item.isChecked ? "log-muted" : ""}`}>{amount(item)}</span>
									</label>
								</li>
							))}
						</ul>
						<div className="flex items-center justify-between pt-4 text-sm">
							<button type="button" onClick={() => setShowBought((value) => !value)} className="text-link">
								{showBought ? "Hide bought items" : `Show bought items (${items.length - left.length})`}
							</button>
							<button type="button" onClick={() => window.print()} className="text-link">
								Print
							</button>
						</div>
					</>
				)}
			</section>
		</div>
	);
}
