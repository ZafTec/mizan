import { getBodyMeasurements } from "@/data/bodyMeasurement";
import { getCurrentGoal, getGoalHistory } from "@/data/goal";
import { getUserServer } from "@/helper/session";
import AddMeasurementForm from "./AddMeasurementForm";
import DeleteMeasurementButton from "./DeleteMeasurementButton";
import MeasurementChart from "./MeasurementChart";
import { DataTable, readPage, readSort, type Column } from "@/components/ui/data-table";

export const dynamic = "force-dynamic";

type Measurement = Awaited<ReturnType<typeof getBodyMeasurements>>["bodyMeasurements"][number];

/** Every measurement is optional, and an em dash reads better than a zero. */
const unit = (value: number | null | undefined, suffix: string) =>
	value ? `${value} ${suffix}` : "—";

const MEASUREMENT_COLUMNS: Column<Measurement>[] = [
	{
		id: "date",
		header: "Date",
		sortKey: "Date",
		cell: (m) => (
			<span className="whitespace-nowrap tabular-nums">
				{new Date(m.date).toLocaleDateString()}
			</span>
		),
	},
	{ id: "weight", header: "Weight", sortKey: "WeightKg", cell: (m) => unit(m.weightKg, "kg") },
	{
		id: "fat",
		header: "Body fat",
		cell: (m) => (m.bodyFatPercentage ? `${m.bodyFatPercentage}%` : "—"),
	},
	{ id: "muscle", header: "Muscle", secondary: true, cell: (m) => unit(m.muscleMassKg, "kg") },
	{ id: "waist", header: "Waist", secondary: true, cell: (m) => unit(m.waistCm, "cm") },
	{ id: "hips", header: "Hips", secondary: true, cell: (m) => unit(m.hipsCm, "cm") },
	{ id: "chest", header: "Chest", secondary: true, cell: (m) => unit(m.chestCm, "cm") },
	{ id: "larm", header: "L arm", secondary: true, cell: (m) => unit(m.leftArmCm, "cm") },
	{ id: "rarm", header: "R arm", secondary: true, cell: (m) => unit(m.rightArmCm, "cm") },
	{ id: "lthigh", header: "L thigh", secondary: true, cell: (m) => unit(m.leftThighCm, "cm") },
	{ id: "rthigh", header: "R thigh", secondary: true, cell: (m) => unit(m.rightThighCm, "cm") },
	{
		id: "notes",
		header: "Notes",
		secondary: true,
		cell: (m) => <span className="line-clamp-1 max-w-[16rem]">{m.notes || "—"}</span>,
	},
	{
		id: "actions",
		header: "",
		align: "right",
		width: "1%",
		cell: (m) => <DeleteMeasurementButton id={m.id} />,
	},
];

function getDelta(
	latest: number | null | undefined,
	previous: number | null | undefined,
): { value: string; positive: boolean } | null {
	if (!latest || !previous) return null;
	const diff = latest - previous;
	return { value: `${diff > 0 ? "+" : ""}${diff.toFixed(1)}`, positive: diff > 0 };
}

export default async function BodyMeasurementsPage({
	searchParams,
}: {
	searchParams: Promise<Record<string, string | undefined>>;
}) {
	await getUserServer();
	const params = await searchParams;
	const page = readPage(params);
	const sort = readSort(params);
	const sortBy = sort.sortBy ?? "Date";
	const sortOrder = sort.sortBy ? sort.sortOrder : "desc";
	const [tableResult, chartResult, goal, goalHist] = await Promise.all([
		getBodyMeasurements(page, 20, sortBy, sortOrder),
		getBodyMeasurements(1, 200, "Date", "desc"),
		getCurrentGoal(),
		getGoalHistory(),
	]);
	const { bodyMeasurements: measurements, totalCount, totalPages } = tableResult;
	const allMeasurements = chartResult.bodyMeasurements;

	const latest = allMeasurements.length > 0 ? allMeasurements[0] : null;
	const previous = allMeasurements.length > 1 ? allMeasurements[1] : null;

	const weightDelta = getDelta(latest?.weightKg, previous?.weightKg);
	const bodyFatDelta = getDelta(latest?.bodyFatPercentage, previous?.bodyFatPercentage);
	const muscleDelta = getDelta(latest?.muscleMassKg, previous?.muscleMassKg);

	return (
		<div className="space-y-6 lg:space-y-8" data-testid="body-measurements-page">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Composition</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Body metrics
					</h1>
				</div>
			</header>

			<div className="grid grid-cols-1 md:grid-cols-3 gap-4">
				<div className="card-hover p-5">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-brand-600 flex items-center justify-center dark:bg-brand-500">
							<i className="ri-scales-3-line text-xl text-white" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
								Weight
							</h3>
							<div className="flex items-center gap-2">
								<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
									{latest?.weightKg ? `${latest.weightKg} kg` : "No data"}
								</p>
								{weightDelta && (
									<span
										className={`text-xs font-medium px-1.5 py-0.5 rounded ${weightDelta.positive ? "bg-orange-50 dark:bg-orange-950 text-orange-600 dark:text-orange-400" : "bg-green-50 dark:bg-green-950 text-green-600 dark:text-green-400"}`}
									>
										{weightDelta.value}
									</span>
								)}
							</div>
						</div>
					</div>
				</div>

				<div className="card-hover p-5">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-accent-600 flex items-center justify-center ">
							<i className="ri-heart-pulse-line text-xl text-white" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
								Body Fat
							</h3>
							<div className="flex items-center gap-2">
								<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
									{latest?.bodyFatPercentage ? `${latest.bodyFatPercentage}%` : "No data"}
								</p>
								{bodyFatDelta && (
									<span
										className={`text-xs font-medium px-1.5 py-0.5 rounded ${bodyFatDelta.positive ? "bg-orange-50 dark:bg-orange-950 text-orange-600 dark:text-orange-400" : "bg-green-50 dark:bg-green-950 text-green-600 dark:text-green-400"}`}
									>
										{bodyFatDelta.value}%
									</span>
								)}
							</div>
						</div>
					</div>
				</div>

				<div className="card-hover p-5">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-charcoal-blue-900 flex items-center justify-center dark:bg-charcoal-blue-100 dark:text-charcoal-blue-900">
							<i className="ri-hand-heart-line text-xl text-current" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
								Muscle Mass
							</h3>
							<div className="flex items-center gap-2">
								<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
									{latest?.muscleMassKg ? `${latest.muscleMassKg} kg` : "No data"}
								</p>
								{muscleDelta && (
									<span
										className={`text-xs font-medium px-1.5 py-0.5 rounded ${muscleDelta.positive ? "bg-green-50 dark:bg-green-950 text-green-600 dark:text-green-400" : "bg-orange-50 dark:bg-orange-950 text-orange-600 dark:text-orange-400"}`}
									>
										{muscleDelta.value}
									</span>
								)}
							</div>
						</div>
					</div>
				</div>
			</div>

			<AddMeasurementForm />

			<MeasurementChart measurements={allMeasurements} goalHistory={goalHist} />

			<section className="card space-y-4 p-6">
				<h2 className="section-title">Measurement history</h2>

				<DataTable
					columns={MEASUREMENT_COLUMNS}
					rows={measurements}
					rowKey={(m) => m.id}
					pathname="/body-measurements"
					searchParams={params}
					sort={sort}
					page={{ page, pageSize: 20, totalCount, totalPages }}
					empty={{
						title: "No measurements yet",
						description: "Record one above and the chart will start filling in.",
					}}
				/>
			</section>
		</div>
	);
}
