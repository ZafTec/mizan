import { Skeleton } from "@/components/ui/skeleton";
import { TableSkeleton } from "@/components/ui/data-table";

export default function Loading() {
	return (
		<div className="space-y-6 lg:space-y-8">
			<div className="space-y-2">
				<Skeleton className="h-3 w-24 rounded" />
				<Skeleton className="h-9 w-72 rounded-lg" />
			</div>
			<Skeleton className="h-40 w-full rounded-3xl" />
			<Skeleton className="h-64 w-full rounded-3xl" />
			<TableSkeleton columns={7} rows={6} toolbar={false} />
		</div>
	);
}
