import { Skeleton } from "@/components/ui/skeleton";

/**
 * What a table looks like before its data arrives.
 *
 * Matching the real column count matters more than it sounds: a skeleton with
 * the wrong shape makes the page jump when the data lands, which reads as a
 * bug rather than as loading.
 */
export default function TableSkeleton({
	columns = 5,
	rows = 8,
	toolbar = true,
}: {
	columns?: number;
	rows?: number;
	toolbar?: boolean;
}) {
	return (
		<div className="space-y-4" aria-busy aria-live="polite">
			<span className="sr-only">Loading…</span>

			{toolbar && (
				<div className="flex flex-wrap gap-2">
					<Skeleton className="h-10 w-full max-w-xs rounded-2xl" />
					<Skeleton className="h-10 w-32 rounded-2xl" />
				</div>
			)}

			<div className="overflow-hidden border border-charcoal-blue-200 dark:border-charcoal-blue-700">
				<div className="flex gap-4 border-b border-charcoal-blue-900 px-4 py-3 dark:border-charcoal-blue-50">
					{Array.from({ length: columns }).map((_, i) => (
						<Skeleton key={i} className="h-3 flex-1 rounded" />
					))}
				</div>

				<div className="divide-y divide-charcoal-blue-100 dark:divide-charcoal-blue-800">
					{Array.from({ length: rows }).map((_, row) => (
						<div key={row} className="flex gap-4 px-4 py-3.5">
							{Array.from({ length: columns }).map((_, col) => (
								<Skeleton
									key={col}
									className="h-4 flex-1 rounded"
									// Uneven widths so it reads as content rather than as a
									// loading bar someone forgot to remove.
									style={{ maxWidth: col === 0 ? "none" : `${60 + ((row + col) % 4) * 10}%` }}
								/>
							))}
						</div>
					))}
				</div>
			</div>

			<Skeleton className="h-4 w-32 rounded" />
		</div>
	);
}

/** A page shell to sit above the table while a route loads. */
export function PageSkeleton({
	columns,
	rows,
	toolbar,
}: {
	columns?: number;
	rows?: number;
	toolbar?: boolean;
}) {
	return (
		<div className="space-y-6 lg:space-y-8">
			<div className="space-y-2">
				<Skeleton className="h-3 w-20 rounded" />
				<Skeleton className="h-9 w-64 rounded-lg" />
				<Skeleton className="h-4 w-40 rounded" />
			</div>
			<TableSkeleton columns={columns} rows={rows} toolbar={toolbar} />
		</div>
	);
}
