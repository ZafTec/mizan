import { Skeleton } from "@/components/ui/skeleton";

/**
 * The shapes a route can be waiting in.
 *
 * A `loading.tsx` per route is what turns a Next.js navigation from "nothing
 * happens for 400ms" into something that acknowledges the click. These are
 * deliberately generic - a skeleton that tries to mirror every element ends up
 * needing maintenance when the page changes, and then it lies.
 *
 * The one thing worth getting right is rough size and count, so the page does
 * not jump when the data lands.
 */
function Header() {
	return (
		<div className="space-y-2">
			<Skeleton className="h-3 w-24 rounded" />
			<Skeleton className="h-9 w-64 rounded-lg" />
			<Skeleton className="h-4 w-80 max-w-full rounded" />
		</div>
	);
}

/** A grid of cards: recipes, exercises, achievements, trainers. */
export function CardGridSkeleton({ count = 6, header = true }: { count?: number; header?: boolean }) {
	return (
		<div className="space-y-6 lg:space-y-8" aria-busy>
			<span className="sr-only">Loading…</span>
			{header && <Header />}
			<div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
				{Array.from({ length: count }).map((_, i) => (
					<div
						key={i}
						className="space-y-3 rounded-3xl border border-charcoal-blue-200 p-4 dark:border-white/10"
					>
						<Skeleton className="h-32 w-full rounded-2xl" />
						<Skeleton className="h-4 w-3/4 rounded" />
						<Skeleton className="h-3 w-1/2 rounded" />
					</div>
				))}
			</div>
		</div>
	);
}

/** A stack of rows: meals, notifications, workouts, feed items. */
export function ListSkeleton({ count = 6, header = true }: { count?: number; header?: boolean }) {
	return (
		<div className="space-y-6 lg:space-y-8" aria-busy>
			<span className="sr-only">Loading…</span>
			{header && <Header />}
			<div className="space-y-2">
				{Array.from({ length: count }).map((_, i) => (
					<div
						key={i}
						className="flex items-center gap-4 rounded-2xl border border-charcoal-blue-200 p-4 dark:border-white/10"
					>
						<Skeleton className="h-10 w-10 shrink-0 rounded-2xl" />
						<div className="min-w-0 flex-1 space-y-2">
							<Skeleton className="h-4 w-1/3 rounded" />
							<Skeleton className="h-3 w-1/2 rounded" />
						</div>
						<Skeleton className="h-4 w-16 rounded" />
					</div>
				))}
			</div>
		</div>
	);
}

/** One record with its panels: a recipe, a client, a household. */
export function DetailSkeleton() {
	return (
		<div className="space-y-6 lg:space-y-8" aria-busy>
			<span className="sr-only">Loading…</span>
			<Header />
			<div className="grid gap-6 lg:grid-cols-3">
				<Skeleton className="h-64 rounded-3xl lg:col-span-2" />
				<Skeleton className="h-64 rounded-3xl" />
			</div>
			<Skeleton className="h-40 rounded-3xl" />
		</div>
	);
}

/** A form: settings, add, edit. */
export function FormSkeleton({ fields = 5 }: { fields?: number }) {
	return (
		<div className="space-y-6 lg:space-y-8" aria-busy>
			<span className="sr-only">Loading…</span>
			<Header />
			<div className="space-y-4 rounded-3xl border border-charcoal-blue-200 p-6 dark:border-white/10">
				{Array.from({ length: fields }).map((_, i) => (
					<div key={i} className="space-y-2">
						<Skeleton className="h-3 w-28 rounded" />
						<Skeleton className="h-11 w-full rounded-2xl" />
					</div>
				))}
				<Skeleton className="h-11 w-32 rounded-2xl" />
			</div>
		</div>
	);
}

/** Stat tiles above whatever else the page holds. */
export function DashboardSkeleton() {
	return (
		<div className="space-y-6 lg:space-y-8" aria-busy>
			<span className="sr-only">Loading…</span>
			<Header />
			<div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
				{Array.from({ length: 4 }).map((_, i) => (
					<Skeleton key={i} className="h-28 rounded-3xl" />
				))}
			</div>
			<div className="grid gap-6 lg:grid-cols-3">
				<Skeleton className="h-72 rounded-3xl lg:col-span-2" />
				<Skeleton className="h-72 rounded-3xl" />
			</div>
		</div>
	);
}
