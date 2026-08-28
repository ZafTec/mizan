import { Skeleton } from "@/components/ui/skeleton";
import { TableSkeleton } from "@/components/ui/data-table";

export default function Loading() {
	return (
		<div className="space-y-6 lg:space-y-8">
			<div className="space-y-2">
				<Skeleton className="h-3 w-24 rounded" />
				<Skeleton className="h-9 w-64 rounded-lg" />
			</div>
			<div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
				{Array.from({ length: 4 }).map((_, i) => (
					<Skeleton key={i} className="h-24 rounded-3xl" />
				))}
			</div>
			<TableSkeleton columns={7} />
		</div>
	);
}
