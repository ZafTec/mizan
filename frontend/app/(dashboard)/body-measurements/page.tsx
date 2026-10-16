import { getBodyMeasurements } from "@/data/bodyMeasurement";
import { getCurrentGoal, getGoalHistory } from "@/data/goal";
import { getUserServer } from "@/helper/session";
import { LogEntryButton } from "@/components/logging/LogEntryButton";
import DeleteMeasurementButton from "./DeleteMeasurementButton";
import MeasurementChart from "./MeasurementChart";
import { DataTable, readPage, readSort, type Column } from "@/components/ui/data-table";

export const dynamic = "force-dynamic";

type Measurement = Awaited<ReturnType<typeof getBodyMeasurements>>["bodyMeasurements"][number];

/** One decimal, never the raw database value; an em dash where nothing was taken. */
const num = (value: number | null | undefined, digits = 1) =>
	value === null || value === undefined ? null : Number(value).toFixed(digits).replace(/\.0$/, "");
const unit = (value: number | null | undefined, suffix: string) => {
	const text = num(value);
	return text === null ? "—" : `${text} ${suffix}`;
};

const MEASUREMENT_COLUMNS: Column<Measurement>[] = [
	{
		id: "date",
		header: "Date",
		sortKey: "Date",
		cell: (m) => (
			<span className="whitespace-nowrap tabular-nums">
				{new Date(m.date).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" })}
			</span>
		),
	},
	{ id: "weight", header: "Weight", sortKey: "WeightKg", align: "right", cell: (m) => unit(m.weightKg, "kg") },
	{ id: "fat", header: "Body fat", align: "right", cell: (m) => (num(m.bodyFatPercentage) === null ? "—" : `${num(m.bodyFatPercentage)}%`) },
	{ id: "muscle", header: "Muscle", align: "right", secondary: true, cell: (m) => unit(m.muscleMassKg, "kg") },
	{ id: "waist", header: "Waist", align: "right", secondary: true, cell: (m) => unit(m.waistCm, "cm") },
	{ id: "hips", header: "Hips", align: "right", secondary: true, cell: (m) => unit(m.hipsCm, "cm") },
	{ id: "chest", header: "Chest", align: "right", secondary: true, cell: (m) => unit(m.chestCm, "cm") },
	{ id: "larm", header: "L arm", align: "right", secondary: true, cell: (m) => unit(m.leftArmCm, "cm") },
	{ id: "rarm", header: "R arm", align: "right", secondary: true, cell: (m) => unit(m.rightArmCm, "cm") },
	{ id: "lthigh", header: "L thigh", align: "right", secondary: true, cell: (m) => unit(m.leftThighCm, "cm") },
	{ id: "rthigh", header: "R thigh", align: "right", secondary: true, cell: (m) => unit(m.rightThighCm, "cm") },
	{
		id: "notes",
		header: "Notes",
		secondary: true,
		cell: (m) => <span className="line-clamp-1 max-w-[16rem]">{m.notes || "—"}</span>,
	},
	{ id: "actions", header: "", align: "right", width: "1%", cell: (m) => <DeleteMeasurementButton id={m.id} /> },
];

/** "−0.2 since last" as words, so the direction is readable without a colour. */
function since(latest: number | null | undefined, previous: number | null | undefined, suffix: string) {
	if (latest === null || latest === undefined || previous === null || previous === undefined) return null;
	const diff = Number(latest) - Number(previous);
	if (Math.abs(diff) < 0.05) return "No change since last";
	return `${diff > 0 ? "+" : "−"}${Math.abs(diff).toFixed(1)} ${suffix} since last`;
}

type Fact = { label: string; value: string | null; unit: string; note: string | null };

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
	const [tableResult, chartResult, , goalHist] = await Promise.all([
		getBodyMeasurements(page, 20, sortBy, sortOrder),
		getBodyMeasurements(1, 200, "Date", "desc"),
		getCurrentGoal(),
		getGoalHistory(),
	]);
	const { bodyMeasurements: measurements, totalCount, totalPages } = tableResult;
	const all = chartResult.bodyMeasurements;

	// The latest value of each field, and the one before it, even when they came from different days.
	const latestOf = (pick: (m: Measurement) => number | null | undefined) => {
		const taken = all.filter((m) => pick(m) !== null && pick(m) !== undefined);
		return [taken[0] ? pick(taken[0]) : null, taken[1] ? pick(taken[1]) : null] as const;
	};
	const [weight, weightBefore] = latestOf((m) => m.weightKg);
	const [fat, fatBefore] = latestOf((m) => m.bodyFatPercentage);
	const [waist, waistBefore] = latestOf((m) => m.waistCm);
	const facts: Fact[] = [
		{ label: "Weight", value: num(weight), unit: "kg", note: since(weight, weightBefore, "kg") },
		{ label: "Body fat", value: num(fat), unit: "%", note: since(fat, fatBefore, "%") },
		{ label: "Waist", value: num(waist), unit: "cm", note: since(waist, waistBefore, "cm") },
	];

	return (
		<div className="log-page space-y-8" data-testid="body-measurements-page">
			<header className="log-page-header block!">
				<h1>Body</h1>
				<p className="log-muted mt-2">Weight and measurements over time.</p>
			</header>

			{all.length > 0 && (
				<section aria-label="Latest measurements" className="facts-strip">
					{facts.map((fact) => (
						<div key={fact.label}>
							<p className="text-sm log-muted">{fact.label}</p>
							<p className="num nutrition-value">
								{fact.value ?? "—"}
								{fact.value !== null && <span className="text-sm font-normal log-muted"> {fact.unit}</span>}
							</p>
							{fact.note && <p className="mt-1 text-xs log-muted">{fact.note}</p>}
						</div>
					))}
				</section>
			)}

			<LogEntryButton kind="measurement" className="btn-primary min-h-11 w-full sm:w-auto">
				Add measurement
			</LogEntryButton>

			{all.length > 0 && <MeasurementChart measurements={all} goalHistory={goalHist} />}

			<section aria-labelledby="history-title" className="space-y-4">
				<header className="log-section-header">
					<h2 id="history-title">History</h2>
				</header>
				<DataTable
					columns={MEASUREMENT_COLUMNS}
					rows={measurements}
					rowKey={(m) => m.id}
					pathname="/body-measurements"
					searchParams={params}
					sort={sort}
					page={{ page, pageSize: 20, totalCount, totalPages }}
					empty={{
						title: "Nothing measured yet",
						description: "Record a weight or a measurement and the chart starts from there.",
					}}
				/>
			</section>
		</div>
	);
}
