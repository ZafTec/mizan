import { getBodyMeasurements } from "@/data/bodyMeasurement";
import { getUserServer } from "@/helper/session";
import AddMeasurementForm from "./AddMeasurementForm";
import DeleteMeasurementButton from "./DeleteMeasurementButton";
import MeasurementChart from "./MeasurementChart";
import SortableHeader from "@/components/SortableHeader";
import Pagination from "@/components/Pagination";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";

export const dynamic = 'force-dynamic';

function getDelta(latest: number | null | undefined, previous: number | null | undefined): { value: string; positive: boolean } | null {
    if (!latest || !previous) return null;
    const diff = latest - previous;
    return { value: `${diff > 0 ? "+" : ""}${diff.toFixed(1)}`, positive: diff > 0 };
}

export default async function BodyMeasurementsPage({
    searchParams,
}: {
    searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
    await getUserServer();
    const params = await searchParams;
    const { page, sortBy, sortOrder } = parseListParams(params, { sortBy: 'Date', sortOrder: 'desc' });
    const [tableResult, chartResult] = await Promise.all([
        getBodyMeasurements(page, 20, sortBy ?? undefined, sortOrder),
        getBodyMeasurements(1, 200, "Date", "desc"),
    ]);
    const { bodyMeasurements: measurements, totalCount, totalPages } = tableResult;
    const allMeasurements = chartResult.bodyMeasurements;
    const baseUrl = buildListUrl('/body-measurements', { sortBy, sortOrder });

    const latest = allMeasurements.length > 0 ? allMeasurements[0] : null;
    const previous = allMeasurements.length > 1 ? allMeasurements[1] : null;

    const weightDelta = getDelta(latest?.weightKg, previous?.weightKg);
    const bodyFatDelta = getDelta(latest?.bodyFatPercentage, previous?.bodyFatPercentage);
    const muscleDelta = getDelta(latest?.muscleMassKg, previous?.muscleMassKg);

    return (
        <div className="space-y-8" data-testid="body-measurements-page">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Body Measurements</h1>
                    <p className="text-slate-500 dark:text-slate-400 mt-1">Track your body composition progress</p>
                </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="card-hover p-5">
                    <div className="flex items-center gap-4">
                        <div className="w-12 h-12 rounded-2xl bg-linear-to-br from-brand-400 to-brand-600 flex items-center justify-center shadow-lg">
                            <i className="ri-scales-3-line text-xl text-white" />
                        </div>
                        <div className="flex-1">
                            <h3 className="font-semibold text-slate-900 dark:text-slate-100">Weight</h3>
                            <div className="flex items-center gap-2">
                                <p className="text-sm text-slate-500 dark:text-slate-400">
                                    {latest?.weightKg ? `${latest.weightKg} kg` : "No data"}
                                </p>
                                {weightDelta && (
                                    <span className={`text-xs font-medium px-1.5 py-0.5 rounded ${weightDelta.positive ? "bg-orange-50 dark:bg-orange-950 text-orange-600 dark:text-orange-400" : "bg-green-50 dark:bg-green-950 text-green-600 dark:text-green-400"}`}>
                                        {weightDelta.value}
                                    </span>
                                )}
                            </div>
                        </div>
                    </div>
                </div>

                <div className="card-hover p-5">
                    <div className="flex items-center gap-4">
                        <div className="w-12 h-12 rounded-2xl bg-linear-to-br from-accent-400 to-accent-600 flex items-center justify-center shadow-lg">
                            <i className="ri-heart-pulse-line text-xl text-white" />
                        </div>
                        <div className="flex-1">
                            <h3 className="font-semibold text-slate-900 dark:text-slate-100">Body Fat</h3>
                            <div className="flex items-center gap-2">
                                <p className="text-sm text-slate-500 dark:text-slate-400">
                                    {latest?.bodyFatPercentage ? `${latest.bodyFatPercentage}%` : "No data"}
                                </p>
                                {bodyFatDelta && (
                                    <span className={`text-xs font-medium px-1.5 py-0.5 rounded ${bodyFatDelta.positive ? "bg-orange-50 dark:bg-orange-950 text-orange-600 dark:text-orange-400" : "bg-green-50 dark:bg-green-950 text-green-600 dark:text-green-400"}`}>
                                        {bodyFatDelta.value}%
                                    </span>
                                )}
                            </div>
                        </div>
                    </div>
                </div>

                <div className="card-hover p-5">
                    <div className="flex items-center gap-4">
                        <div className="w-12 h-12 rounded-2xl bg-linear-to-br from-violet-400 to-violet-600 flex items-center justify-center shadow-lg">
                            <i className="ri-hand-heart-line text-xl text-white" />
                        </div>
                        <div className="flex-1">
                            <h3 className="font-semibold text-slate-900 dark:text-slate-100">Muscle Mass</h3>
                            <div className="flex items-center gap-2">
                                <p className="text-sm text-slate-500 dark:text-slate-400">
                                    {latest?.muscleMassKg ? `${latest.muscleMassKg} kg` : "No data"}
                                </p>
                                {muscleDelta && (
                                    <span className={`text-xs font-medium px-1.5 py-0.5 rounded ${muscleDelta.positive ? "bg-green-50 dark:bg-green-950 text-green-600 dark:text-green-400" : "bg-orange-50 dark:bg-orange-950 text-orange-600 dark:text-orange-400"}`}>
                                        {muscleDelta.value}
                                    </span>
                                )}
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <AddMeasurementForm />

            <MeasurementChart measurements={allMeasurements} />

            <div className="card p-6">
                <h2 className="section-title mb-6">Measurement History</h2>
                {measurements.length > 0 ? (
                    <div className="overflow-x-auto">
                        <table className="w-full">
                            <thead className="border-b border-slate-200 dark:border-slate-700">
                                <tr>
                                    <SortableHeader sortKey="Date" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="text-left py-3 px-4 text-sm font-semibold text-slate-700 dark:text-slate-300">Date</SortableHeader>
                                    <SortableHeader sortKey="WeightKg" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="text-left py-3 px-4 text-sm font-semibold text-slate-700 dark:text-slate-300">Weight</SortableHeader>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700 dark:text-slate-300">Body Fat</th>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700 dark:text-slate-300">Muscle</th>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700 dark:text-slate-300">Waist</th>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700 dark:text-slate-300">L Arm</th>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700 dark:text-slate-300">R Arm</th>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700 dark:text-slate-300">L Thigh</th>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700 dark:text-slate-300">R Thigh</th>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700 dark:text-slate-300">Notes</th>
                                    <th className="w-10"></th>
                                </tr>
                            </thead>
                            <tbody>
                                {measurements.map((m) => (
                                    <tr key={m.id} className="border-b border-slate-100 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800">
                                        <td className="py-3 px-4 text-sm text-slate-900 dark:text-slate-100">
                                            {new Date(m.date).toLocaleDateString()}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600 dark:text-slate-400">
                                            {m.weightKg ? `${m.weightKg} kg` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600 dark:text-slate-400">
                                            {m.bodyFatPercentage ? `${m.bodyFatPercentage}%` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600 dark:text-slate-400">
                                            {m.muscleMassKg ? `${m.muscleMassKg} kg` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600 dark:text-slate-400">
                                            {m.waistCm ? `${m.waistCm} cm` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600 dark:text-slate-400">
                                            {m.leftArmCm ? `${m.leftArmCm} cm` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600 dark:text-slate-400">
                                            {m.rightArmCm ? `${m.rightArmCm} cm` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600 dark:text-slate-400">
                                            {m.leftThighCm ? `${m.leftThighCm} cm` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600 dark:text-slate-400">
                                            {m.rightThighCm ? `${m.rightThighCm} cm` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600 dark:text-slate-400 max-w-xs truncate">
                                            {m.notes || "-"}
                                        </td>
                                        <td className="py-3 px-4">
                                            <DeleteMeasurementButton id={m.id} />
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : (
                    <div className="text-center py-16">
                        <div className="w-16 h-16 rounded-2xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center mx-auto mb-4">
                            <i className="ri-body-scan-line text-3xl text-slate-400 dark:text-slate-500" />
                        </div>
                        <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">No measurements yet</h3>
                        <p className="text-slate-500 dark:text-slate-400">Start tracking your progress today!</p>
                    </div>
                )}
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
