"use client";

import { useRouter } from "next/navigation";
import { useState, useCallback, useEffect } from "react";
import { useDebounce } from "@/lib/hooks/useDebounce";

const TAG_SUGGESTIONS = [
	"HIGH-PROTEIN",
	"LOW-CARB",
	"QUICK",
	"VEGAN",
	"VEGETARIAN",
	"GLUTEN-FREE",
] as const;

const TAG_ICONS: Record<string, string> = {
	"HIGH-PROTEIN": "ri-heart-pulse-line",
	"LOW-CARB": "ri-leaf-line",
	"QUICK": "ri-timer-flash-line",
	"VEGAN": "ri-plant-line",
	"VEGETARIAN": "ri-seedling-line",
	"GLUTEN-FREE": "ri-forbid-line",
};

const SORT_OPTIONS = [
	{ label: "Newest", sortBy: "createdAt", sortOrder: "desc" },
	{ label: "Oldest", sortBy: "createdAt", sortOrder: "asc" },
	{ label: "Title A-Z", sortBy: "title", sortOrder: "asc" },
	{ label: "Title Z-A", sortBy: "title", sortOrder: "desc" },
	{ label: "P/Cal High\u2192Low", sortBy: "proteinCalorieRatio", sortOrder: "desc" },
	{ label: "P/Cal Low\u2192High", sortBy: "proteinCalorieRatio", sortOrder: "asc" },
] as const;

interface RecipeFiltersProps {
	currentSearch?: string;
	currentTags?: string[];
	currentSortBy?: string;
	currentSortOrder?: string;
}

export default function RecipeFilters({
	currentSearch = "",
	currentTags = [],
	currentSortBy,
	currentSortOrder,
}: RecipeFiltersProps) {
	const router = useRouter();
	const [search, setSearch] = useState(currentSearch);
	const debouncedSearch = useDebounce(search, 400);

	const buildUrl = useCallback(
		(overrides: { search?: string; tags?: string[]; sortBy?: string; sortOrder?: string }) => {
			const params = new URLSearchParams();
			const s = overrides.search ?? debouncedSearch;
			const t = overrides.tags ?? currentTags;
			const sb = overrides.sortBy ?? currentSortBy;
			const so = overrides.sortOrder ?? currentSortOrder;

			if (s) params.set("search", s);
			t.forEach((tag) => params.append("tags", tag));
			if (sb) params.set("sortBy", sb);
			if (so) params.set("sortOrder", so);

			const qs = params.toString();
			return qs ? `/recipes?${qs}` : "/recipes";
		},
		[debouncedSearch, currentTags, currentSortBy, currentSortOrder],
	);

	useEffect(() => {
		if (debouncedSearch !== currentSearch) {
			router.push(buildUrl({ search: debouncedSearch }));
		}
	}, [debouncedSearch, currentSearch, router, buildUrl]);

	const toggleTag = (tag: string) => {
		const next = currentTags.includes(tag)
			? currentTags.filter((t) => t !== tag)
			: [...currentTags, tag];
		router.push(buildUrl({ tags: next }));
	};

	const handleSort = (sortBy: string, sortOrder: string) => {
		router.push(buildUrl({ sortBy, sortOrder }));
	};

	const currentSortIndex = SORT_OPTIONS.findIndex(
		(o) => o.sortBy === currentSortBy && o.sortOrder === currentSortOrder,
	);

	const hasActiveFilters = currentTags.length > 0 || currentSortIndex >= 0;

	return (
		<div className="space-y-4">
			{/* Search bar */}
			<div className="flex items-center gap-3 w-full px-4 py-3 rounded-xl border border-slate-200 bg-white dark:bg-slate-800 dark:border-slate-700 transition-all duration-200 focus-within:ring-2 focus-within:ring-brand-500/20 focus-within:border-brand-500 dark:focus-within:border-brand-400">
				<i className="ri-search-line text-slate-400 text-lg shrink-0" />
				<input
					type="text"
					placeholder="Search by name, description, or tag..."
					value={search}
					onChange={(e) => setSearch(e.target.value)}
					className="flex-1 bg-transparent text-charcoal-blue-900 dark:text-charcoal-blue-100 placeholder-slate-400 dark:placeholder-slate-500 outline-none text-base min-w-0"
				/>
				{search && (
					<button
						type="button"
						onClick={() => setSearch("")}
						className="text-slate-400 hover:text-slate-600 transition-colors shrink-0"
					>
						<i className="ri-close-circle-fill text-lg" />
					</button>
				)}
			</div>

			{/* Tags row */}
			<div className="flex flex-wrap gap-2">
				{TAG_SUGGESTIONS.map((tag) => {
					const active = currentTags.includes(tag);
					const icon = TAG_ICONS[tag];
					return (
						<button
							key={tag}
							type="button"
							onClick={() => toggleTag(tag)}
							className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-medium transition-all border ${
								active
									? "bg-brand-500 text-white border-brand-500 shadow-sm shadow-brand-500/25"
									: "bg-white dark:bg-slate-900 text-slate-600 dark:text-slate-400 border-slate-200 dark:border-slate-700 hover:border-brand-300 hover:text-brand-600"
							}`}
						>
							{icon && <i className={`${icon} text-base`} />}
							{tag.replace("-", " ").toLowerCase().replace(/\b\w/g, c => c.toUpperCase())}
							{active && <i className="ri-close-line text-sm ml-0.5 opacity-70" />}
						</button>
					);
				})}
			</div>

			{/* Sort row */}
			<div className="flex flex-wrap items-center gap-1.5">
				<span className="text-xs text-charcoal-blue-400 dark:text-charcoal-blue-500 font-medium mr-1">Sort:</span>
				{SORT_OPTIONS.map((opt, i) => (
					<button
						key={opt.label}
						type="button"
						onClick={() => handleSort(opt.sortBy, opt.sortOrder)}
						className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-colors ${
							currentSortIndex === i
								? "bg-slate-900 text-white dark:bg-slate-100 dark:text-slate-900"
								: "bg-slate-100 text-slate-500 hover:bg-slate-200 hover:text-slate-700 dark:bg-slate-800 dark:text-slate-400 dark:hover:bg-slate-700"
						}`}
					>
						{opt.label}
					</button>
				))}
			</div>

			{/* Active filter summary */}
			{hasActiveFilters && (
				<div className="flex items-center justify-between">
					<p className="text-xs text-charcoal-blue-400 dark:text-charcoal-blue-500">
						{currentTags.length > 0 && `${currentTags.length} tag${currentTags.length > 1 ? "s" : ""} selected`}
						{currentTags.length > 0 && currentSortIndex >= 0 && " · "}
						{currentSortIndex >= 0 && `Sorted by ${SORT_OPTIONS[currentSortIndex].label}`}
					</p>
					<button
						type="button"
						onClick={() => router.push(search ? `/recipes?search=${encodeURIComponent(search)}` : "/recipes")}
						className="text-xs text-brand-500 hover:text-brand-600 font-medium transition-colors"
					>
						Clear filters
					</button>
				</div>
			)}
		</div>
	);
}
