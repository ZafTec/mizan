"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";
import { Icon } from "@/components/ui/icon";
import type { NavGroup } from "@/components/Layout/nav";

/**
 * Tier 3 is a long list by design, which is exactly when a directory stops
 * being scannable. Filtering happens on the client because the whole list is
 * already here - a round trip to search sixty static strings would be slower
 * than typing.
 */
export default function MoreDirectory({ groups }: { groups: NavGroup[] }) {
	const [query, setQuery] = useState("");
	const inputRef = useRef<HTMLInputElement>(null);

	// "/" to search is the convention people already have from every other
	// directory. Ignored while typing somewhere else.
	useEffect(() => {
		function onKeyDown(event: KeyboardEvent) {
			if (event.key !== "/" || event.metaKey || event.ctrlKey || event.altKey) return;
			const active = document.activeElement;
			const typing =
				active instanceof HTMLInputElement ||
				active instanceof HTMLTextAreaElement ||
				(active instanceof HTMLElement && active.isContentEditable);
			if (typing) return;
			event.preventDefault();
			inputRef.current?.focus();
		}

		window.addEventListener("keydown", onKeyDown);
		return () => window.removeEventListener("keydown", onKeyDown);
	}, []);

	const filtered = useMemo(() => {
		const needle = query.trim().toLowerCase();
		if (!needle) return groups;

		// Matching the group name too, so "admin" finds the whole section
		// rather than only the items that repeat the word.
		return groups
			.map((group) => {
				const groupMatches = group.label.toLowerCase().includes(needle);
				return {
					...group,
					items: group.items.filter(
						(item) =>
							groupMatches ||
							item.label.toLowerCase().includes(needle) ||
							item.description?.toLowerCase().includes(needle) ||
							item.href.toLowerCase().includes(needle),
					),
				};
			})
			.filter((group) => group.items.length > 0);
	}, [groups, query]);

	const resultCount = filtered.reduce((total, group) => total + group.items.length, 0);
	const searching = query.trim().length > 0;

	return (
		<div className="space-y-8">
			<div className="relative">
				<span className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-charcoal-blue-400">
					<Icon name="search" size={16} aria-hidden="true" />
				</span>
				<input
					ref={inputRef}
					type="search"
					value={query}
					onChange={(event) => setQuery(event.target.value)}
					placeholder="Search features…"
					aria-label="Search features"
					className="input w-full !rounded-2xl !py-3 !pl-11 !pr-11"
					autoComplete="off"
				/>
				{searching && (
					<button
						type="button"
						onClick={() => {
							setQuery("");
							inputRef.current?.focus();
						}}
						aria-label="Clear search"
						className="absolute right-3 top-1/2 -translate-y-1/2 rounded-lg p-1.5 text-charcoal-blue-400 transition-colors hover:bg-charcoal-blue-100 hover:text-charcoal-blue-700 dark:hover:bg-white/10 dark:hover:text-charcoal-blue-100"
					>
						<Icon name="x" size={14} />
					</button>
				)}
			</div>

			{searching && (
				<p aria-live="polite" className="-mt-4 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{resultCount === 0
						? "Nothing matches that."
						: `${resultCount} ${resultCount === 1 ? "result" : "results"}`}
				</p>
			)}

			{resultCount === 0 && searching ? (
				<div className="surface-panel flex flex-col items-center gap-3 p-10 text-center">
					<span className="icon-chip h-12 w-12 text-charcoal-blue-400">
						<Icon name="search" size={20} />
					</span>
					<div className="space-y-1">
						<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
							No feature called &ldquo;{query.trim()}&rdquo;
						</p>
						<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Try a shorter word, or browse the groups below.
						</p>
					</div>
					<button type="button" onClick={() => setQuery("")} className="btn-secondary !rounded-2xl !py-2 text-sm">
						Clear search
					</button>
				</div>
			) : (
				filtered.map((group) => (
					<section key={group.label} className="space-y-3">
						<h2 className="text-[11px] font-semibold uppercase tracking-[0.18em] text-charcoal-blue-400">
							{group.label}
						</h2>
						<div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
							{group.items.map((item) => (
								<Link
									key={item.href}
									href={item.href}
									className="surface-panel group flex items-start gap-3 p-4 transition-all duration-150 hover:-translate-y-0.5 hover:border-brand-500/40 hover:bg-brand-50/40 hover:shadow-lg hover:shadow-brand-900/5 active:translate-y-0 dark:hover:bg-white/5"
								>
									<span className="icon-chip h-10 w-10 shrink-0 text-brand-600 transition-colors group-hover:bg-brand-500/15 dark:text-brand-400">
										<Icon name={item.icon} size={18} />
									</span>
									<span className="min-w-0 flex-1">
										<span className="block font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
											{item.label}
										</span>
										{item.description && (
											<span className="block text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
												{item.description}
											</span>
										)}
									</span>
									<span className="mt-0.5 shrink-0 text-charcoal-blue-300 opacity-0 transition-all duration-150 group-hover:translate-x-0.5 group-hover:opacity-100 dark:text-charcoal-blue-600">
										<Icon name="arrowRight" size={14} aria-hidden="true" />
									</span>
								</Link>
							))}
						</div>
					</section>
				))
			)}
		</div>
	);
}
