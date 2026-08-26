import Link from "next/link";
import { getExercises } from "@/data/exercise";
import {
	DataTable,
	Pill,
	TableToolbar,
	readPage,
	readSort,
	type Column,
} from "@/components/ui/data-table";
import ExerciseAdminActions from "./ExerciseAdminActions";

export const dynamic = "force-dynamic";

const PAGE_SIZE = 20;

type Exercise = Awaited<ReturnType<typeof getExercises>>["exercises"][number];

const columns: Column<Exercise>[] = [
	{
		id: "name",
		header: "Name",
		sortKey: "name",
		cell: (e) => (
			<div className="min-w-0">
				<p className="truncate font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{e.name}
				</p>
				{e.description && (
					<p className="line-clamp-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
						{e.description}
					</p>
				)}
			</div>
		),
	},
	{ id: "category", header: "Category", sortKey: "category", cell: (e) => <Pill tone="info">{e.category}</Pill> },
	{ id: "muscle", header: "Muscle group", sortKey: "muscleGroup", secondary: true, cell: (e) => e.muscleGroup || "—" },
	{ id: "equipment", header: "Equipment", secondary: true, cell: (e) => e.equipment || "—" },
	{
		id: "type",
		header: "Type",
		align: "center",
		cell: (e) => <Pill tone={e.isCustom ? "warn" : "neutral"}>{e.isCustom ? "Custom" : "System"}</Pill>,
	},
	{
		id: "actions",
		header: "",
		align: "right",
		width: "1%",
		cell: (e) => <ExerciseAdminActions id={e.id} custom={Boolean(e.isCustom)} />,
	},
];

export default async function AdminExercisesPage({
	searchParams,
}: {
	searchParams: Promise<Record<string, string | undefined>>;
}) {
	const params = await searchParams;
	const page = readPage(params);
	const sort = readSort(params);

	const { exercises, totalPages, totalCount } = await getExercises(
		params.search || undefined,
		params.category || undefined,
		page,
		PAGE_SIZE,
		sort.sortBy ?? undefined,
		sort.sortOrder,
	);

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Moderation</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Exercises
					</h1>
				</div>
				<Link href="/workouts" className="btn-secondary !rounded-2xl">
					Back to workouts
				</Link>
			</header>

			<TableToolbar
				searchPlaceholder="Search exercises…"
				filters={[
					{
						name: "category",
						label: "Any category",
						options: ["Strength", "Cardio", "Flexibility", "Balance"].map((v) => ({
							value: v,
							label: v,
						})),
					},
				]}
			/>

			<DataTable
				columns={columns}
				rows={exercises}
				rowKey={(e) => e.id}
				pathname="/admin/exercises"
				searchParams={params}
				sort={sort}
				page={{ page, pageSize: PAGE_SIZE, totalCount, totalPages }}
				empty={{
					title: "No exercises match",
					description: "Try a different search or clear the filters.",
				}}
			/>
		</div>
	);
}
