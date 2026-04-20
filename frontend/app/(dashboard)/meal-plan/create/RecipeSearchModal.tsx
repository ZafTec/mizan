"use client";

import { useState, useEffect } from "react";
import { createPortal } from "react-dom";
import { clientApi } from "@/lib/api.client";

interface RecipeResult {
	id: string;
	title: string;
	nutrition?: {
		caloriesPerServing?: number;
		proteinGrams?: number;
	};
}

interface RecipeSearchModalProps {
	onSelect: (recipe: RecipeResult, servings: number) => void;
	onClose: () => void;
}

export default function RecipeSearchModal({ onSelect, onClose }: RecipeSearchModalProps) {
	const [query, setQuery] = useState("");
	const [results, setResults] = useState<RecipeResult[]>([]);
	const [loading, setLoading] = useState(false);
	const [servings, setServings] = useState<Record<string, number>>({});
	const [mounted, setMounted] = useState(false);

	useEffect(() => {
		setMounted(true);
	}, []);

	useEffect(() => {
		const prev = document.body.style.overflow;
		document.body.style.overflow = "hidden";
		return () => {
			document.body.style.overflow = prev;
		};
	}, []);

	useEffect(() => {
		const timer = setTimeout(async () => {
			if (!query.trim()) {
				setResults([]);
				return;
			}
			setLoading(true);
			try {
				const data = await clientApi<{ items: RecipeResult[] }>(
					`/api/Recipes?SearchTerm=${encodeURIComponent(query)}&IncludePublic=true&PageSize=10`
				);
				setResults(Array.isArray(data.items) ? data.items : []);
			} catch {
				setResults([]);
			} finally {
				setLoading(false);
			}
		}, 300);
		return () => clearTimeout(timer);
	}, [query]);

	if (!mounted) return null;

	const modal = (
		<div className="fixed inset-0 z-[100] overflow-y-auto bg-black/50" onClick={onClose}>
			<div className="flex min-h-full items-center justify-center p-4">
				<div className="w-full max-w-lg rounded-2xl bg-white shadow-xl dark:bg-charcoal-blue-950" onClick={(e) => e.stopPropagation()}>
					<div className="border-b border-charcoal-blue-200 p-4 dark:border-white/10">
						<div className="flex items-center gap-3">
							<i className="ri-search-line text-charcoal-blue-400 dark:text-charcoal-blue-500" />
							<input
								type="text"
								value={query}
								onChange={(e) => setQuery(e.target.value)}
								placeholder="Search recipes..."
								className="flex-1 bg-transparent text-sm text-charcoal-blue-900 outline-none placeholder:text-charcoal-blue-400 dark:text-charcoal-blue-100 dark:placeholder:text-charcoal-blue-500"
								autoFocus
							/>
							<button onClick={onClose} className="text-charcoal-blue-400 hover:text-charcoal-blue-600 dark:text-charcoal-blue-500 dark:hover:text-charcoal-blue-300">
								<i className="ri-close-line text-xl" />
							</button>
						</div>
					</div>
					<div className="max-h-80 overflow-y-auto p-2">
						{loading && (
							<div className="py-8 text-center text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">Searching...</div>
						)}
						{!loading && query && results.length === 0 && (
							<div className="py-8 text-center text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">No recipes found</div>
						)}
						{!loading && !query && (
							<div className="py-8 text-center text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">Type to search recipes</div>
						)}
						{results.map((recipe) => (
							<div key={recipe.id} className="flex items-center gap-3 rounded-xl p-3 transition-colors hover:bg-charcoal-blue-50 dark:hover:bg-charcoal-blue-900/60">
								<div className="flex-1 min-w-0">
									<p className="truncate text-sm font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">{recipe.title}</p>
									<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
										{recipe.nutrition?.caloriesPerServing || 0} kcal
										{recipe.nutrition?.proteinGrams ? ` · ${recipe.nutrition.proteinGrams.toFixed(0)}g protein` : ""}
									</p>
								</div>
								<div className="flex items-center gap-2">
									<input
										type="number"
										min={1}
										value={servings[recipe.id] ?? 1}
										onChange={(e) => setServings({ ...servings, [recipe.id]: Math.max(1, parseInt(e.target.value) || 1) })}
										className="w-14 rounded-lg border border-charcoal-blue-200 bg-white px-2 py-1 text-center text-sm text-charcoal-blue-900 dark:border-white/10 dark:bg-charcoal-blue-900 dark:text-charcoal-blue-100"
									/>
									<button
										onClick={() => onSelect(recipe, servings[recipe.id] ?? 1)}
										className="rounded-lg bg-brand-600 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-brand-700 dark:bg-brand-500 dark:text-charcoal-blue-900 dark:hover:bg-brand-400"
									>
										Add
									</button>
								</div>
							</div>
						))}
					</div>
				</div>
			</div>
		</div>
	);

	return createPortal(modal, document.body);
}
