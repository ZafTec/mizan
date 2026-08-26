import Link from "next/link";
import type { ReactNode } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { cn } from "@/lib/utils";
import type { Column, TablePage, TableSort } from "./types";
import { nextSort, withParams, withParamsResettingPage } from "./url";

interface DataTableProps<T> {
	columns: Column<T>[];
	rows: T[];
	rowKey: (row: T) => string;

	/** Where the header links point. Everything else in the query string is preserved. */
	pathname: string;
	searchParams: Record<string, string | undefined>;

	sort?: TableSort;
	page?: TablePage;

	/** Shown instead of the table when there is nothing. Never an empty grid. */
	empty?: { title: string; description?: string; action?: ReactNode };

	/** A row-level link. Makes the whole row clickable without nesting anchors in cells. */
	href?: (row: T) => string;

	caption?: ReactNode;
}

const ALIGN = {
	left: "text-left",
	right: "text-right",
	center: "text-center",
} as const;

/**
 * The one table.
 *
 * Six admin screens hand-rolled `<table>` with their own sort links, their own
 * pagination and their own idea of what an empty state looks like. This is
 * that, once.
 *
 * A server component on purpose. These tables are server-paginated - the page
 * holds twenty rows out of thousands - so a client grid library would sort and
 * filter the wrong set while adding a bundle. Sorting and filtering are links
 * that change the query string; the server does the work and the URL stays
 * shareable.
 */
export default function DataTable<T>({
	columns,
	rows,
	rowKey,
	pathname,
	searchParams,
	sort,
	page,
	empty,
	href,
	caption,
}: DataTableProps<T>) {
	if (rows.length === 0 && empty) {
		return <EmptyState {...empty} />;
	}

	return (
		<div className="space-y-4">
			<div className="overflow-x-auto rounded-3xl border border-charcoal-blue-200 dark:border-white/10">
				<table className="w-full min-w-[36rem] border-collapse text-sm">
					{caption && (
						<caption className="caption-bottom px-4 py-3 text-left text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
							{caption}
						</caption>
					)}
					<thead className="bg-charcoal-blue-50/80 dark:bg-white/[0.03]">
						<tr>
							{columns.map((column) => (
								<th
									key={column.id}
									scope="col"
									style={column.width ? { width: column.width } : undefined}
									className={cn(
										"px-4 py-3 text-xs font-medium uppercase tracking-wide text-charcoal-blue-500 dark:text-charcoal-blue-400",
										ALIGN[column.align ?? "left"],
										column.secondary && "hidden sm:table-cell",
									)}
									aria-sort={ariaSort(column, sort)}
								>
									{column.sortKey && sort ? (
										<SortLink
											column={column}
											sort={sort}
											pathname={pathname}
											searchParams={searchParams}
										/>
									) : (
										column.header
									)}
								</th>
							))}
						</tr>
					</thead>

					<tbody className="divide-y divide-charcoal-blue-200 dark:divide-white/10">
						{rows.map((row) => (
							<tr
								key={rowKey(row)}
								className="transition-colors hover:bg-charcoal-blue-50/60 dark:hover:bg-white/[0.02]"
							>
								{columns.map((column, index) => (
									<td
										key={column.id}
										className={cn(
											"px-4 py-3 text-charcoal-blue-700 dark:text-charcoal-blue-200",
											ALIGN[column.align ?? "left"],
											column.secondary && "hidden sm:table-cell",
										)}
									>
										{href && index === 0 ? (
											<Link href={href(row)} className="hover:underline">
												{column.cell(row)}
											</Link>
										) : (
											column.cell(row)
										)}
									</td>
								))}
							</tr>
						))}
					</tbody>
				</table>
			</div>

			{page && <Pager page={page} pathname={pathname} searchParams={searchParams} />}
		</div>
	);
}

function ariaSort<T>(column: Column<T>, sort?: TableSort): "ascending" | "descending" | "none" | undefined {
	if (!column.sortKey || !sort) return undefined;
	if (sort.sortBy !== column.sortKey) return "none";
	return sort.sortOrder === "asc" ? "ascending" : "descending";
}

function SortLink<T>({
	column,
	sort,
	pathname,
	searchParams,
}: {
	column: Column<T>;
	sort: TableSort;
	pathname: string;
	searchParams: Record<string, string | undefined>;
}) {
	const next = nextSort(column.sortKey!, sort);
	const active = sort.sortBy === column.sortKey;

	return (
		<Link
			href={withParamsResettingPage(pathname, searchParams, {
				sortBy: next.sortBy,
				sortOrder: next.sortBy ? next.sortOrder : null,
			})}
			className={cn(
				"inline-flex items-center gap-1 transition-colors hover:text-charcoal-blue-900 dark:hover:text-charcoal-blue-50",
				active && "text-charcoal-blue-900 dark:text-charcoal-blue-50",
			)}
		>
			{column.header}
			<AnimatedIcon
				name={active && sort.sortOrder === "desc" ? "arrowRight" : "arrowRight"}
				size={11}
				className={cn(
					"transition-transform",
					active ? (sort.sortOrder === "asc" ? "-rotate-90" : "rotate-90") : "opacity-30 -rotate-90",
				)}
			/>
		</Link>
	);
}

function Pager({
	page,
	pathname,
	searchParams,
}: {
	page: TablePage;
	pathname: string;
	searchParams: Record<string, string | undefined>;
}) {
	if (page.totalPages <= 1) {
		return (
			<p className="px-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				{page.totalCount} {page.totalCount === 1 ? "row" : "rows"}
			</p>
		);
	}

	const from = (page.page - 1) * page.pageSize + 1;
	const to = Math.min(page.page * page.pageSize, page.totalCount);

	return (
		<div className="flex flex-wrap items-center justify-between gap-3 px-1">
			<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				{from}–{to} of {page.totalCount}
			</p>

			<div className="flex items-center gap-1">
				<PageLink
					pathname={pathname}
					searchParams={searchParams}
					page={page.page - 1}
					disabled={page.page <= 1}
					label="Previous"
				/>
				<span className="px-2 text-xs tabular-nums text-charcoal-blue-600 dark:text-charcoal-blue-300">
					{page.page} / {page.totalPages}
				</span>
				<PageLink
					pathname={pathname}
					searchParams={searchParams}
					page={page.page + 1}
					disabled={page.page >= page.totalPages}
					label="Next"
				/>
			</div>
		</div>
	);
}

function PageLink({
	pathname,
	searchParams,
	page,
	disabled,
	label,
}: {
	pathname: string;
	searchParams: Record<string, string | undefined>;
	page: number;
	disabled: boolean;
	label: string;
}) {
	const className =
		"rounded-xl border border-charcoal-blue-200 px-3 py-1.5 text-xs transition-colors dark:border-white/10";

	if (disabled) {
		return (
			<span aria-disabled className={cn(className, "opacity-40")}>
				{label}
			</span>
		);
	}

	return (
		<Link
			href={withParams(pathname, searchParams, { page })}
			className={cn(className, "hover:border-verdigris-400 hover:text-verdigris-700 dark:hover:text-verdigris-300")}
		>
			{label}
		</Link>
	);
}

export function EmptyState({
	title,
	description,
	action,
}: {
	title: string;
	description?: string;
	action?: ReactNode;
}) {
	return (
		<div className="rounded-3xl border border-dashed border-charcoal-blue-200 p-10 text-center dark:border-white/10">
			<h3 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
				{title}
			</h3>
			{description && (
				<p className="mx-auto mt-1 max-w-sm text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{description}
				</p>
			)}
			{action && <div className="mt-4">{action}</div>}
		</div>
	);
}
