"use client";

import { useState, useRef, useEffect, useMemo } from "react";
import { clientApi } from "@/lib/api.client";
import { useDebounce } from "@/lib/hooks/useDebounce";

interface RecipeSearchResult {
	id: string;
	title: string;
	nutrition?: {
		caloriesPerServing?: number | null;
		proteinGrams?: number | null;
		carbsGrams?: number | null;
		fatGrams?: number | null;
		fiberGrams?: number | null;
	};
}

interface RecipeIngredientSearchProps {
	value: string;
	onChange: (value: string) => void;
	onSelect: (recipe: RecipeSearchResult) => void;
}

export default function RecipeIngredientSearch({
	value,
	onChange,
	onSelect,
}: RecipeIngredientSearchProps) {
	const [fetchedResults, setFetchedResults] = useState<RecipeSearchResult[]>([]);
	const [isOpen, setIsOpen] = useState(false);
	const debouncedQuery = useDebounce(value, 300);
	const containerRef = useRef<HTMLDivElement>(null);

	const results = useMemo(
		() => (!debouncedQuery || debouncedQuery.length < 2) ? [] : fetchedResults,
		[debouncedQuery, fetchedResults],
	);

	useEffect(() => {
		if (!debouncedQuery || debouncedQuery.length < 2) return;
		let cancelled = false;
		const params = new URLSearchParams({
			SearchTerm: debouncedQuery,
			IncludePublic: "true",
			PageSize: "6",
		});
		clientApi<{ items: RecipeSearchResult[] }>(`/api/Recipes?${params}`)
			.then((data) => {
				if (!cancelled) {
					setFetchedResults(data.items || []);
					setIsOpen(true);
				}
			})
			.catch(() => {
				if (!cancelled) setFetchedResults([]);
			});
		return () => {
			cancelled = true;
		};
	}, [debouncedQuery]);

	useEffect(() => {
		function handleClickOutside(event: MouseEvent) {
			if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
				setIsOpen(false);
			}
		}
		document.addEventListener("mousedown", handleClickOutside);
		return () => document.removeEventListener("mousedown", handleClickOutside);
	}, []);

	return (
		<div ref={containerRef} className="relative">
			<input
				type="text"
				placeholder="Search recipes..."
				value={value}
				onChange={(e) => {
					onChange(e.target.value);
					setIsOpen(true);
				}}
				onFocus={() => results.length > 0 && setIsOpen(true)}
				className="input w-full"
			/>
			{isOpen && results.length > 0 && (
				<div className="absolute z-50 w-full mt-1 bg-white dark:bg-charcoal-blue-900 border border-charcoal-blue-200 dark:border-charcoal-blue-800 rounded-xl shadow-lg overflow-hidden max-h-60 overflow-y-auto">
					{results.map((recipe) => (
						<button
							key={recipe.id}
							type="button"
							onClick={() => {
								onSelect(recipe);
								setIsOpen(false);
							}}
							className="w-full p-3 text-left hover:bg-charcoal-blue-50 dark:hover:bg-charcoal-blue-800 flex items-center justify-between border-b border-charcoal-blue-100 dark:border-white/10 last:border-0"
						>
							<span className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100 truncate">
								{recipe.title}
							</span>
							<span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400 whitespace-nowrap ml-2">
								{recipe.nutrition?.caloriesPerServing?.toFixed(0) || 0} kcal/srv
							</span>
						</button>
					))}
				</div>
			)}
		</div>
	);
}

export type { RecipeSearchResult };
