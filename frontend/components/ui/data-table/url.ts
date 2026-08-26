import type { TableSort } from "./types";

/**
 * Every table control is a link that edits the current query string, so a
 * filtered, sorted page is a URL you can bookmark, share, or hit refresh on.
 * That is also what keeps these tables server-rendered.
 */
export function withParams(
	pathname: string,
	current: URLSearchParams | Record<string, string | undefined>,
	changes: Record<string, string | number | null | undefined>,
): string {
	const params =
		current instanceof URLSearchParams
			? new URLSearchParams(current)
			: new URLSearchParams(
					Object.entries(current).filter((e): e is [string, string] => e[1] !== undefined),
				);

	for (const [key, value] of Object.entries(changes)) {
		if (value === null || value === undefined || value === "") params.delete(key);
		else params.set(key, String(value));
	}

	const query = params.toString();
	return query ? `${pathname}?${query}` : pathname;
}

/**
 * Sorting or filtering while on page 7 of the old result set lands you on a
 * page 7 that no longer means anything - usually an empty one, which reads as
 * "no results".
 */
export function withParamsResettingPage(
	pathname: string,
	current: URLSearchParams | Record<string, string | undefined>,
	changes: Record<string, string | number | null | undefined>,
): string {
	return withParams(pathname, current, { ...changes, page: null });
}

/** Click a sorted column to reverse it, click again to clear it. */
export function nextSort(columnKey: string, sort: TableSort): TableSort {
	if (sort.sortBy !== columnKey) return { sortBy: columnKey, sortOrder: "asc" };
	if (sort.sortOrder === "asc") return { sortBy: columnKey, sortOrder: "desc" };
	return { sortBy: null, sortOrder: "asc" };
}

export function readSort(params: Record<string, string | undefined>): TableSort {
	return {
		sortBy: params.sortBy ?? null,
		sortOrder: params.sortOrder === "desc" ? "desc" : "asc",
	};
}

export function readPage(params: Record<string, string | undefined>, fallback = 1): number {
	const parsed = Number.parseInt(params.page ?? "", 10);
	return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}
