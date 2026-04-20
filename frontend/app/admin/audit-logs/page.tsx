import { getAuditLogs } from "@/data/audit";
import Link from "next/link";
import { format } from "date-fns";
import Pagination from "@/components/Pagination";
import SortableHeader from "@/components/SortableHeader";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";

export default async function AuditLogsPage({
    searchParams,
}: {
    searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
    const params = await searchParams;
    const { page, sortBy, sortOrder } = parseListParams(params, { sortBy: 'Timestamp', sortOrder: 'desc' });
    const action = params.action as string | undefined;
    const entityType = params.entityType as string | undefined;

    const { logs, totalCount, totalPages } = await getAuditLogs({
        page,
        pageSize: 20,
        action,
        entityType,
        sortBy: sortBy ?? undefined,
        sortOrder,
    });

    const baseUrl = buildListUrl('/admin/audit-logs', { action, entityType });

    return (
        <div className="space-y-6 lg:space-y-8">
            <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div className="space-y-2">
                    <p className="eyebrow">Security</p>
                    <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
                        Audit logs
                    </h1>
                    <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                        Monitor system activities and user actions.
                    </p>
                </div>
                <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-charcoal-blue-500 dark:text-charcoal-blue-400">Total:</span>
                    <span className="px-2.5 py-0.5 rounded-full bg-charcoal-blue-100 dark:bg-charcoal-blue-900 text-charcoal-blue-700 dark:text-charcoal-blue-200 text-sm font-bold">{totalCount}</span>
                </div>
            </header>

            {/* Filters */}
            <div className="card p-4">
                <form className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div>
                        <label className="label text-xs uppercase tracking-wider font-bold text-charcoal-blue-400 dark:text-charcoal-blue-500">Action Name</label>
                        <input
                            name="action"
                            type="text"
                            placeholder="Filter by action..."
                            defaultValue={action}
                            className="input text-sm"
                        />
                    </div>
                    <div>
                        <label className="label text-xs uppercase tracking-wider font-bold text-charcoal-blue-400 dark:text-charcoal-blue-500">Entity Type</label>
                        <input
                            name="entityType"
                            type="text"
                            placeholder="Filter by entity..."
                            defaultValue={entityType}
                            className="input text-sm"
                        />
                    </div>
                    <div className="flex items-end">
                        <button type="submit" className="btn-primary w-full h-10">
                            Apply Filters
                        </button>
                    </div>
                </form>
            </div>

            {/* Table */}
            <div className="card overflow-hidden">
                <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                        <thead>
                            <tr className="bg-charcoal-blue-50/50 dark:bg-charcoal-blue-800/50 border-b border-charcoal-blue-100 dark:border-white/10">
                                <SortableHeader sortKey="timestamp" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-charcoal-blue-500 dark:text-charcoal-blue-400">Timestamp</SortableHeader>
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-charcoal-blue-500 dark:text-charcoal-blue-400">User</th>
                                <SortableHeader sortKey="action" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-charcoal-blue-500 dark:text-charcoal-blue-400">Action</SortableHeader>
                                <SortableHeader sortKey="entityType" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-charcoal-blue-500 dark:text-charcoal-blue-400">Entity</SortableHeader>
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-charcoal-blue-500 dark:text-charcoal-blue-400">IP Address</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
                            {logs.length === 0 ? (
                                <tr>
                                    <td colSpan={5} className="px-6 py-12 text-center text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                        No audit logs found.
                                    </td>
                                </tr>
                            ) : (
                                logs.map((log) => (
                                    <tr key={log.id} className="hover:bg-charcoal-blue-50/50 dark:hover:bg-charcoal-blue-800/50 transition-colors group">
                                        <td className="px-6 py-4">
                                            <div className="text-sm font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">
                                                {format(new Date(log.timestamp), "MMM d, yyyy")}
                                            </div>
                                            <div className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400 font-mono">
                                                {format(new Date(log.timestamp), "HH:mm:ss")}
                                            </div>
                                        </td>
                                        <td className="px-6 py-4">
                                            <div className="text-sm text-charcoal-blue-900 dark:text-charcoal-blue-100 font-medium">{log.userEmail || "System"}</div>
                                            <div className="text-xs text-charcoal-blue-400 dark:text-charcoal-blue-500 font-mono">{log.userId || ""}</div>
                                        </td>
                                        <td className="px-6 py-4">
                                            <span className="px-2 py-1 rounded-md bg-blue-50 dark:bg-blue-950 text-blue-700 dark:text-blue-300 text-xs font-bold border border-blue-100 dark:border-blue-800">
                                                {log.action}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4">
                                            <div className="text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300 font-medium">
                                                {log.entityType}
                                            </div>
                                            <div className="text-xs text-charcoal-blue-400 dark:text-charcoal-blue-500 font-mono">
                                                {log.entityId}
                                            </div>
                                        </td>
                                        <td className="px-6 py-4 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400 font-mono">
                                            {log.ipAddress || "—"}
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
