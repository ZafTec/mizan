import { getBodyMeasurements } from "@/data/bodyMeasurement";
import { getUserServer } from "@/helper/session";
import AddMeasurementForm from "./AddMeasurementForm";
import SortableHeader from "@/components/SortableHeader";
import Pagination from "@/components/Pagination";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";

export const dynamic = 'force-dynamic';

export default async function BodyMeasurementsPage({
    searchParams,
}: {
    searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
    await getUserServer();
    const params = await searchParams;
    const { page, sortBy, sortOrder } = parseListParams(params, { sortBy: 'Date', sortOrder: 'desc' });
    const { bodyMeasurements: measurements, totalCount, totalPages } = await getBodyMeasurements(page, 20, sortBy ?? undefined, sortOrder);
    const baseUrl = buildListUrl('/body-measurements', {});

    const latest = measurements.length > 0 ? measurements[0] : null;

    return (
        <div className="space-y-8">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900">Body Measurements</h1>
                    <p className="text-slate-500 mt-1">Track your body composition progress</p>
                </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="card-hover p-5">
                    <div className="flex items-center gap-4">
                        <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-brand-400 to-brand-600 flex items-center justify-center shadow-lg">
                            <i className="ri-scales-3-line text-xl text-white" />
                        </div>
                        <div className="flex-1">
                            <h3 className="font-semibold text-slate-900">Weight</h3>
                            <p className="text-sm text-slate-500">
                                {latest?.weightKg ? `${latest.weightKg} kg` : "No data"}
                            </p>
                        </div>
                    </div>
                </div>

                <div className="card-hover p-5">
                    <div className="flex items-center gap-4">
                        <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-accent-400 to-accent-600 flex items-center justify-center shadow-lg">
                            <i className="ri-heart-pulse-line text-xl text-white" />
                        </div>
                        <div className="flex-1">
                            <h3 className="font-semibold text-slate-900">Body Fat</h3>
                            <p className="text-sm text-slate-500">
                                {latest?.bodyFatPercentage ? `${latest.bodyFatPercentage}%` : "No data"}
                            </p>
                        </div>
                    </div>
                </div>

                <div className="card-hover p-5">
                    <div className="flex items-center gap-4">
                        <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-violet-400 to-violet-600 flex items-center justify-center shadow-lg">
                            <i className="ri-hand-heart-line text-xl text-white" />
                        </div>
                        <div className="flex-1">
                            <h3 className="font-semibold text-slate-900">Muscle Mass</h3>
                            <p className="text-sm text-slate-500">
                                {latest?.muscleMassKg ? `${latest.muscleMassKg} kg` : "No data"}
                            </p>
                        </div>
                    </div>
                </div>
            </div>

            <AddMeasurementForm />

            <div className="card p-6">
                <h2 className="section-title mb-6">Measurement History</h2>
                {measurements.length > 0 ? (
                    <div className="overflow-x-auto">
                        <table className="w-full">
                            <thead className="border-b border-slate-200">
                                <tr>
                                    <SortableHeader sortKey="Date" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="text-left py-3 px-4 text-sm font-semibold text-slate-700">Date</SortableHeader>
                                    <SortableHeader sortKey="WeightKg" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="text-left py-3 px-4 text-sm font-semibold text-slate-700">Weight</SortableHeader>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700">Body Fat</th>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700">Muscle</th>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700">Waist</th>
                                    <th className="text-left py-3 px-4 text-sm font-semibold text-slate-700">Notes</th>
                                </tr>
                            </thead>
                            <tbody>
                                {measurements.map((m) => (
                                    <tr key={m.id} className="border-b border-slate-100 hover:bg-slate-50">
                                        <td className="py-3 px-4 text-sm text-slate-900">
                                            {new Date(m.date).toLocaleDateString()}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600">
                                            {m.weightKg ? `${m.weightKg} kg` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600">
                                            {m.bodyFatPercentage ? `${m.bodyFatPercentage}%` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600">
                                            {m.muscleMassKg ? `${m.muscleMassKg} kg` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600">
                                            {m.waistCm ? `${m.waistCm} cm` : "-"}
                                        </td>
                                        <td className="py-3 px-4 text-sm text-slate-600 max-w-xs truncate">
                                            {m.notes || "-"}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : (
                    <div className="text-center py-16">
                        <div className="w-16 h-16 rounded-2xl bg-slate-100 flex items-center justify-center mx-auto mb-4">
                            <i className="ri-body-scan-line text-3xl text-slate-400" />
                        </div>
                        <h3 className="text-lg font-semibold text-slate-900 mb-2">No measurements yet</h3>
                        <p className="text-slate-500">Start tracking your progress today!</p>
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
