import { getAuditLogs } from "@/data/audit";
import Link from "next/link";
import { format } from "date-fns";
import Pagination from "@/components/Pagination";

export default async function AuditLogsPage({
    searchParams,
}: {
    searchParams: Promise<{ page?: string; action?: string; entityType?: string }>;
}) {
    const params = await searchParams;
    const page = parseInt(params.page || "1");
    const action = params.action;
    const entityType = params.entityType;

    const { logs, totalCount } = await getAuditLogs({
        page,
        pageSize: 20,
        action,
        entityType,
    });

    const totalPages = Math.ceil(totalCount / 20);

    return (
        <div className="space-y-6">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900">Audit Logs</h1>
                    <p className="text-slate-500 mt-1">Monitor system activities and user actions</p>
                </div>
                <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-slate-500">Total Activities:</span>
                    <span className="px-2.5 py-0.5 rounded-full bg-slate-100 text-slate-700 text-sm font-bold">{totalCount}</span>
                </div>
            </div>

            {/* Filters */}
            <div className="card p-4">
                <form className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div>
                        <label className="label text-xs uppercase tracking-wider font-bold text-slate-400">Action Name</label>
                        <input
                            name="action"
                            type="text"
                            placeholder="Filter by action..."
                            defaultValue={action}
                            className="input text-sm"
                        />
                    </div>
                    <div>
                        <label className="label text-xs uppercase tracking-wider font-bold text-slate-400">Entity Type</label>
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
                            <tr className="bg-slate-50/50 border-b border-slate-100">
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500">Timestamp</th>
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500">User</th>
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500">Action</th>
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500">Entity</th>
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500">IP Address</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-100">
                            {logs.length === 0 ? (
                                <tr>
                                    <td colSpan={5} className="px-6 py-12 text-center text-slate-500">
                                        No audit logs found.
                                    </td>
                                </tr>
                            ) : (
                                logs.map((log) => (
                                    <tr key={log.id} className="hover:bg-slate-50/50 transition-colors group">
                                        <td className="px-6 py-4">
                                            <div className="text-sm font-medium text-slate-900">
                                                {format(new Date(log.timestamp), "MMM d, yyyy")}
                                            </div>
                                            <div className="text-xs text-slate-500 font-mono">
                                                {format(new Date(log.timestamp), "HH:mm:ss")}
                                            </div>
                                        </td>
                                        <td className="px-6 py-4">
                                            <div className="text-sm text-slate-900 font-medium">{log.userEmail || "System"}</div>
                                            <div className="text-xs text-slate-400 font-mono">{log.userId || ""}</div>
                                        </td>
                                        <td className="px-6 py-4">
                                            <span className="px-2 py-1 rounded-md bg-blue-50 text-blue-700 text-xs font-bold border border-blue-100">
                                                {log.action}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4">
                                            <div className="text-sm text-slate-700 font-medium">
                                                {log.entityType}
                                            </div>
                                            <div className="text-xs text-slate-400 font-mono">
                                                {log.entityId}
                                            </div>
                                        </td>
                                        <td className="px-6 py-4 text-sm text-slate-500 font-mono">
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
                    baseUrl="/admin/audit-logs"
                />
            )}
        </div>
    );
}
