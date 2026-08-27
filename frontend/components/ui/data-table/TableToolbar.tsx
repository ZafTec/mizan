"use client";

import { useEffect, useState, useTransition } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Icon } from "@/components/ui/icon";
import { withParamsResettingPage } from "./url";

export interface SelectFilter {
	name: string;
	label: string;
	options: { value: string; label: string }[];
}

/**
 * Search and filters, as URL edits.
 *
 * The only client component in the table stack, and only because a text input
 * needs to debounce. Everything it does ends up in the query string, so the
 * result is still a server-rendered page you can link someone to.
 */
export default function TableToolbar({
	searchPlaceholder,
	searchParam = "search",
	filters = [],
	children,
}: {
	/** Omit when the endpoint has no search - a dead search box is worse than none. */
	searchPlaceholder?: string;
	searchParam?: string;
	filters?: SelectFilter[];
	children?: React.ReactNode;
}) {
	const router = useRouter();
	const pathname = usePathname();
	const params = useSearchParams();
	const [, startTransition] = useTransition();
	const [term, setTerm] = useState(params.get(searchParam) ?? "");

	// Debounced, so typing does not fire a request per keystroke. Skipped when
	// the box already agrees with the URL - otherwise a back button press
	// would immediately push the old value forward again.
	useEffect(() => {
		if (!searchPlaceholder) return;
		const current = params.get(searchParam) ?? "";
		if (term === current) return;

		const timer = setTimeout(() => {
			startTransition(() => {
				router.replace(
					withParamsResettingPage(pathname, params.toString() ? new URLSearchParams(params.toString()) : new URLSearchParams(), {
						[searchParam]: term || null,
					}),
					{ scroll: false },
				);
			});
		}, 350);

		return () => clearTimeout(timer);
	}, [term, params, pathname, router, searchParam, searchPlaceholder]);

	function setFilter(name: string, value: string) {
		startTransition(() => {
			router.replace(
				withParamsResettingPage(pathname, new URLSearchParams(params.toString()), {
					[name]: value || null,
				}),
				{ scroll: false },
			);
		});
	}

	const active = filters.some((f) => params.get(f.name)) || term.length > 0;

	return (
		<div className="flex flex-wrap items-center gap-2">
			{searchPlaceholder && (
				<div className="relative min-w-0 flex-1 sm:max-w-xs">
					<span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-charcoal-blue-400">
						<Icon name="search" size={14} />
					</span>
					<input
						type="search"
						value={term}
						onChange={(e) => setTerm(e.target.value)}
						placeholder={searchPlaceholder}
						aria-label={searchPlaceholder}
						className="input w-full !rounded-2xl !py-2 !pl-9 text-sm"
					/>
				</div>
			)}

			{filters.map((filter) => (
				<select
					key={filter.name}
					value={params.get(filter.name) ?? ""}
					onChange={(e) => setFilter(filter.name, e.target.value)}
					aria-label={filter.label}
					className="input !w-auto !rounded-2xl !py-2 text-sm"
				>
					<option value="">{filter.label}</option>
					{filter.options.map((option) => (
						<option key={option.value} value={option.value}>
							{option.label}
						</option>
					))}
				</select>
			))}

			{active && (
				<button
					type="button"
					onClick={() => {
						setTerm("");
						startTransition(() => router.replace(pathname, { scroll: false }));
					}}
					className="btn-ghost btn-sm"
				>
					Clear
				</button>
			)}

			{children && <div className="ml-auto flex items-center gap-2">{children}</div>}
		</div>
	);
}
