"use client";

import { useEffect, useState, useCallback } from "react";
import { fetchLiveAuditLogs } from "@/actions/audit";
import { AuditLog } from "@/data/audit";
import { formatDistanceToNow } from "date-fns";

const INTERVALS = [
    { label: "Off", value: 0 },
    { label: "5s", value: 5000 },
    { label: "10s", value: 10000 },
    { label: "30s", value: 30000 },
    { label: "1m", value: 60000 },
];

export default function LiveAuditLog() {
    const [logs, setLogs] = useState<AuditLog[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [refreshInterval, setRefreshInterval] = useState(10000); // Default 10s
    const [lastUpdated, setLastUpdated] = useState<Date>(new Date());
    const [countdown, setCountdown] = useState(10);

    const loadLogs = useCallback(async () => {
        setIsLoading(true);
        try {
            const result = await fetchLiveAuditLogs(1, 10);
            setLogs(result.logs);
            setLastUpdated(new Date());
            setCountdown(refreshInterval / 1000); // Reset countdown after successful load
        } catch (error) {
            console.error("Failed to fetch live audit logs:", error);
            setLogs([]); // Clear logs on error
        } finally {
            setIsLoading(false);
        }
    }, [refreshInterval]);

    // Initial load
    useEffect(() => {
        loadLogs();
    }, [loadLogs]);

    // Reset countdown when interval changes
    useEffect(() => {
        setCountdown(refreshInterval / 1000);
    }, [refreshInterval]);

    // Handling Polling
    useEffect(() => {
        if (refreshInterval === 0) return;

        const intervalId = setInterval(() => {
            loadLogs();
        }, refreshInterval);

        return () => clearInterval(intervalId);
    }, [refreshInterval, loadLogs]);

    // Handling Countdown
    useEffect(() => {
        if (refreshInterval === 0 || isLoading) return;

        const timerId = setInterval(() => {
            setCountdown((prev) => Math.max(0, prev - 1));
        }, 1000);

        return () => clearInterval(timerId);
    }, [refreshInterval, isLoading]);

    return (
        <div className="bg-card rounded-lg border shadow-sm flex flex-col h-full overflow-hidden">
            <div className="p-4 border-b flex items-center justify-between bg-slate-50/50">
                <div className="flex items-center gap-2">
                    <div className={`w-2 h-2 rounded-full ${refreshInterval > 0 ? 'bg-green-500 animate-pulse' : 'bg-slate-300'}`} />
                    <h2 className="text-lg font-semibold">Live Activity</h2>
                </div>

                <div className="flex items-center gap-3">
                    {refreshInterval > 0 && !isLoading && (
                        <span className="text-xs text-muted-foreground tabular-nums lg:hidden xl:inline">
                            Next refresh in {countdown}s
                        </span>
                    )}
                    <div className="flex bg-slate-100 rounded-md p-0.5">
                        {INTERVALS.map((interval) => (
                            <button
                                key={interval.value}
                                onClick={() => setRefreshInterval(interval.value)}
                                className={`px-2 py-1 text-xs rounded-sm transition-all ${refreshInterval === interval.value
                                        ? "bg-white text-primary shadow-sm font-medium"
                                        : "text-muted-foreground hover:text-foreground"
                                    }`}
                            >
                                {interval.label}
                            </button>
                        ))}
                    </div>
                    <button
                        onClick={() => loadLogs()}
                        disabled={isLoading}
                        className="p-1.5 hover:bg-slate-200 rounded-md transition-colors disabled:opacity-50"
                        title="Refresh Now"
                    >
                        <i className={`ri-refresh-line ${isLoading ? 'animate-spin' : ''}`} />
                    </button>
                </div>
            </div>

            <div className="flex-1 overflow-y-auto min-h-100">
                {logs.length === 0 && !isLoading ? (
                    <div className="p-8 text-center text-muted-foreground">
                        No recent activity found.
                    </div>
                ) : (
                    <div className="divide-y">
                        {logs.map((log) => (
                            <div key={log.id} className="p-3 hover:bg-slate-50 transition-colors group">
                                <div className="flex justify-between items-start mb-1">
                                    <span className="text-xs font-bold px-1.5 py-0.5 rounded bg-brand-50 text-brand-700 uppercase tracking-wider">
                                        {log.action.replace("Command", "")}
                                    </span>
                                    <span className="text-[10px] text-muted-foreground">
                                        {formatDistanceToNow(new Date(log.timestamp), { addSuffix: true })}
                                    </span>
                                </div>
                                <div className="text-sm">
                                    <span className="font-medium">{log.userEmail || 'System'}</span>
                                    <span className="text-muted-foreground mx-1">performed</span>
                                    <span className="font-medium">{log.entityType}</span>
                                    <span className="text-slate-400 font-mono text-[10px] ml-1">#{log.entityId.split('-')[0]}</span>
                                </div>
                                {log.ipAddress && (
                                    <div className="text-[10px] text-slate-400 mt-1 flex items-center gap-1">
                                        <i className="ri-map-pin-line text-[12px]" />
                                        {log.ipAddress}
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>
                )}
            </div>

            <div className="p-3 border-t bg-slate-50/30 flex justify-between items-center">
                <span className="text-[10px] text-muted-foreground">
                    Last updated: {lastUpdated.toLocaleTimeString()}
                </span>
                <a href="/admin/audit-logs" className="text-xs text-primary hover:underline font-medium">
                    View full history →
                </a>
            </div>
        </div>
    );
}
