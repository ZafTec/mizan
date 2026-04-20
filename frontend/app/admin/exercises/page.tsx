import { getExercises } from "@/data/exercise";
import Pagination from "@/components/Pagination";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";
import Link from "next/link";

export const dynamic = "force-dynamic";

export default async function AdminExercisesPage({
	searchParams,
}: {
	searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
	const params = await searchParams;
	const { page, sortBy, sortOrder } = parseListParams(params);
	const searchTerm = params.search as string | undefined;
	const category = params.category as string | undefined;

	const { exercises, totalPages, totalCount } = await getExercises(
		searchTerm,
		category,
		page,
		20,
		sortBy ?? undefined,
		sortOrder,
	);

	const listParams: Record<string, string | undefined> = { search: searchTerm, category };
	const baseUrl = buildListUrl("/admin/exercises", listParams);

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Moderation</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Manage exercises
					</h1>
					<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Browse and manage the exercise library.
					</p>
				</div>
				<Link href="/workouts" className="btn-secondary">
					<i className="ri-arrow-left-line" />
					Back to Workouts
				</Link>
			</header>

			<div className="flex flex-col md:flex-row gap-4 items-center justify-between">
				<form className="flex gap-3 w-full md:w-auto">
					<div className="relative flex-1 md:w-72">
						<i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-charcoal-blue-400" />
						<input
							name="search"
							type="search"
							placeholder="Search exercises..."
							defaultValue={searchTerm}
							className="input pl-10 h-11 w-full"
						/>
					</div>
					<select name="category" defaultValue={category || ""} className="input h-11 w-40">
						<option value="">All Categories</option>
						<option value="Strength">Strength</option>
						<option value="Cardio">Cardio</option>
						<option value="Flexibility">Flexibility</option>
						<option value="Balance">Balance</option>
					</select>
					<button type="submit" className="btn-primary h-11">Filter</button>
				</form>
				<div className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					<span className="font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">{totalCount}</span> exercises
				</div>
			</div>

			<div className="card overflow-hidden">
				<div className="overflow-x-auto">
					<table className="w-full text-left border-collapse">
						<thead>
							<tr className="border-b border-charcoal-blue-100 bg-charcoal-blue-50/50 dark:border-white/10 dark:bg-charcoal-blue-900/60">
								<th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Name</th>
								<th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Category</th>
								<th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Muscle Group</th>
								<th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Equipment</th>
								<th className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Type</th>
							</tr>
						</thead>
						<tbody className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
							{exercises.length === 0 ? (
								<tr>
									<td colSpan={5} className="px-6 py-12 text-center text-charcoal-blue-500 dark:text-charcoal-blue-400">
										No exercises found.
									</td>
								</tr>
							) : (
								exercises.map((exercise) => (
									<tr key={exercise.id} className="group transition-colors hover:bg-charcoal-blue-50/50 dark:hover:bg-charcoal-blue-900/60">
										<td className="px-6 py-4">
											<div className="text-sm font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">
												{exercise.name}
											</div>
											{exercise.description && (
												<div className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400 line-clamp-1">{exercise.description}</div>
											)}
										</td>
										<td className="px-6 py-4">
											<span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium bg-brand-100 text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
												{exercise.category}
											</span>
										</td>
										<td className="px-6 py-4 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
											{exercise.muscleGroup || "-"}
										</td>
										<td className="px-6 py-4 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
											{exercise.equipment || "-"}
										</td>
										<td className="px-6 py-4 text-center">
											<span className="inline-flex items-center gap-1 rounded-full border border-charcoal-blue-200 bg-charcoal-blue-100 px-2 py-0.5 text-xs font-bold text-charcoal-blue-500 dark:border-white/10 dark:bg-charcoal-blue-900/60 dark:text-charcoal-blue-300">
												System
											</span>
										</td>
									</tr>
								))
							)}
						</tbody>
					</table>
				</div>
			</div>

			{totalPages > 1 && (
				<Pagination
					currentPage={page}
					totalPages={totalPages}
					totalCount={totalCount}
					pageSize={20}
					baseUrl={baseUrl}
				/>
			)}
		</div>
	);
}
