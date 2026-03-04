"use client";

import { useRouter } from "next/navigation";
import { useState, useCallback } from "react";
import { useDebounce } from "@/lib/hooks/useDebounce";
import { useEffect } from "react";

const TAG_SUGGESTIONS = [
	"HIGH-PROTEIN",
	"LOW-CARB",
	"QUICK",
	"VEGAN",
	"VEGETARIAN",
	"GLUTEN-FREE",
] as const;

const SORT_OPTIONS = [
	{ label: "Newest", sortBy: "createdAt", sortOrder: "desc" },
	{ label: "Oldest", sortBy: "createdAt", sortOrder: "asc" },
	{ label: "Title A-Z", sortBy: "title", sortOrder: "asc" },
	{ label: "Title Z-A", sortBy: "title", sortOrder: "desc" },
	{ label: "P/Cal High→Low", sortBy: "proteinCalorieRatio", sortOrder: "desc" },
	{ label: "P/Cal Low→High", sortBy: "proteinCalorieRatio", sortOrder: "asc" },
] as const;

interface RecipeFiltersProps {
	currentSearch?: string;
	currentTags?: string[];
	currentSortBy?: string;
	currentSortOrder?: string;
	currentMinPcal?: number;
}

export default function RecipeFilters({
	currentSearch = "",
	currentTags = [],
	currentSortBy,
	currentSortOrder,
	currentMinPcal,
}: RecipeFiltersProps) {
	const router = useRouter();
	const [search, setSearch] = useState(currentSearch);
	const [minPcal, setMinPcal] = useState(currentMinPcal?.toString() ?? "");
	const debouncedSearch = useDebounce(search, 400);
	const debouncedMinPcal = useDebounce(minPcal, 500);

	const buildUrl = useCallback(
		(overrides: { search?: string; tags?: string[]; sortBy?: string; sortOrder?: string; minPcal?: string }) => {
			const params = new URLSearchParams();
			const s = overrides.search ?? debouncedSearch;
			const t = overrides.tags ?? currentTags;
			const sb = overrides.sortBy ?? currentSortBy;
			const so = overrides.sortOrder ?? currentSortOrder;
			const mp = overrides.minPcal ?? debouncedMinPcal;

			if (s) params.set("search", s);
			t.forEach((tag) => params.append("tags", tag));
			if (sb) params.set("sortBy", sb);
			if (so) params.set("sortOrder", so);
			const mpNum = parseInt(mp);
			if (!isNaN(mpNum) && mpNum > 0) params.set("minPcal", String(mpNum));

			const qs = params.toString();
			return qs ? `/recipes?${qs}` : "/recipes";
		},
		[debouncedSearch, currentTags, currentSortBy, currentSortOrder, debouncedMinPcal],
	);

	useEffect(() => {
		if (debouncedSearch !== currentSearch) {
			router.push(buildUrl({ search: debouncedSearch }));
		}
	}, [debouncedSearch, currentSearch, router, buildUrl]);

	useEffect(() => {
		const parsed = parseInt(debouncedMinPcal);
		const currentVal = currentMinPcal ?? 0;
		const newVal = isNaN(parsed) ? 0 : parsed;
		if (newVal !== currentVal) {
			router.push(buildUrl({ minPcal: debouncedMinPcal }));
		}
	}, [debouncedMinPcal, currentMinPcal, router, buildUrl]);

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

	return (
		<div className="card p-4 space-y-4">
			<div className="flex flex-col sm:flex-row gap-3">
				<div className="relative flex-1">
					<i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
					<input
						type="text"
						placeholder="Search recipes..."
						value={search}
						onChange={(e) => setSearch(e.target.value)}
						className="input pl-10 w-full"
					/>
				</div>
				<div className="flex items-center gap-2">
					<label className="text-sm font-medium text-slate-600 dark:text-slate-400 whitespace-nowrap">
						Min P/Cal
					</label>
					<div className="relative w-20">
						<input
							type="number"
							min={0}
							max={100}
							placeholder="0"
							value={minPcal}
							onChange={(e) => setMinPcal(e.target.value)}
							className="input text-center pr-6"
						/>
						<span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs text-slate-400">%</span>
					</div>
				</div>
				<select
					value={currentSortIndex >= 0 ? currentSortIndex : ""}
					onChange={handleSort}
					className="input w-full sm:w-48"
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

			<div className="flex flex-wrap gap-2">
				{TAG_SUGGESTIONS.map((tag) => {
					const active = currentTags.includes(tag);
					return (
						<button
							key={tag}
							type="button"
							onClick={() => toggleTag(tag)}
							className={`px-3 py-1.5 rounded-full text-sm font-medium transition-colors ${
								active
									? "bg-brand-500 text-white"
									: "bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-slate-700"
							}`}
						>
							{tag}
						</button>
					);
				})}
			</div>
		</div>
	);
}
