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

	const handleSort = (e: React.ChangeEvent<HTMLSelectElement>) => {
		const opt = SORT_OPTIONS[Number(e.target.value)];
		if (opt) router.push(buildUrl({ sortBy: opt.sortBy, sortOrder: opt.sortOrder }));
	};

	const currentSortIndex = SORT_OPTIONS.findIndex(
		(o) => o.sortBy === currentSortBy && o.sortOrder === currentSortOrder,
	);

	const hasActiveFilters = currentTags.length > 0 || currentSortIndex >= 0;

	return (
		<div className="space-y-4">
			{/* Search + Sort row */}
			<div className="flex flex-col sm:flex-row gap-3">
				<div className="relative flex-1">
					<i className="ri-search-line absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 text-lg" />
					<input
						type="text"
						placeholder="Search by name, description, or tag..."
						value={search}
						onChange={(e) => setSearch(e.target.value)}
						className="input pl-11 py-3 w-full text-base"
					/>
					{search && (
						<button
							type="button"
							onClick={() => setSearch("")}
							className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 transition-colors"
						>
							<i className="ri-close-circle-fill text-lg" />
						</button>
					)}
				</div>
				<select
					value={currentSortIndex >= 0 ? currentSortIndex : ""}
					onChange={handleSort}
					className="input py-3 w-full sm:w-52"
				>
					<option value="" disabled>
						Sort by...
					</option>
					{SORT_OPTIONS.map((opt, i) => (
						<option key={opt.label} value={i}>
							{opt.label}
						</option>
					))}
				</select>
			</div>

			{/* Tags */}
			<div className="flex flex-wrap gap-2">
				{TAG_SUGGESTIONS.map((tag) => {
					const active = currentTags.includes(tag);
					const icon = TAG_ICONS[tag];
					return (
						<button
							key={tag}
							type="button"
							onClick={() => toggleTag(tag)}
							className={`inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-sm font-medium transition-all border ${
								active
									? "bg-brand-500 text-white border-brand-500 shadow-sm shadow-brand-500/25"
									: "bg-white dark:bg-slate-900 text-slate-600 dark:text-slate-400 border-slate-200 dark:border-slate-700 hover:border-brand-300 hover:text-brand-600 dark:hover:border-brand-700 dark:hover:text-brand-400"
							}`}
						>
							{icon && <i className={`${icon} text-base`} />}
							{tag.replace("-", " ").toLowerCase().replace(/\b\w/g, c => c.toUpperCase())}
							{active && <i className="ri-close-line text-sm ml-0.5 opacity-70" />}
						</button>
					);
				})}
			</div>

			{/* Active filter summary */}
			{hasActiveFilters && (
				<div className="flex items-center justify-between">
					<p className="text-xs text-slate-400 dark:text-slate-500">
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
