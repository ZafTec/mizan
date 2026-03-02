"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useState, useCallback, useEffect } from "react";
import { useDebounce } from "@/lib/hooks/useDebounce";

export default function IngredientFilters({ currentMinPcal }: { currentMinPcal?: number }) {
	const router = useRouter();
	const searchParams = useSearchParams();
	const [minPcal, setMinPcal] = useState(currentMinPcal?.toString() ?? "");
	const debouncedMinPcal = useDebounce(minPcal, 500);

	const buildUrl = useCallback(
		(mp: string) => {
			const params = new URLSearchParams(searchParams.toString());
			params.delete("minPcal");
			params.delete("page");
			const v = parseInt(mp);
			if (!isNaN(v) && v > 0) params.set("minPcal", String(v));
			const qs = params.toString();
			return qs ? `/ingredients?${qs}` : "/ingredients";
		},
		[searchParams],
	);

	useEffect(() => {
		const parsed = parseInt(debouncedMinPcal);
		const currentVal = currentMinPcal ?? 0;
		const newVal = isNaN(parsed) ? 0 : parsed;
		if (newVal !== currentVal) {
			router.push(buildUrl(debouncedMinPcal));
		}
	}, [debouncedMinPcal, currentMinPcal, router, buildUrl]);

	return (
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
	);
}
