"use client";

import { useEffect, useState } from "react";
import ConfirmationModal from "@/components/ConfirmationModal";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Icon } from "@/components/ui/icon";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { appToast } from "@/lib/toast";
import { useMcpAnalytics, useMcpConnections } from "@/lib/hooks/useMcpConnections";
import { householdSummary, summarizeAccess } from "@/lib/mcp-permissions";
import type { McpConnection } from "@/types/mcp";
import { EditAccessDialog } from "./EditAccessDialog";
import { AddAppGuide } from "./AddAppGuide";

const formatWhen = (value?: string | null) =>
	value
		? new Date(value).toLocaleString("en-US", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })
		: "Never";

export default function McpPage() {
	const { connections, groups, loading, error, load, update, disconnect } = useMcpConnections();
	const { analytics, loading: analyticsLoading, load: loadAnalytics } = useMcpAnalytics();
	const [editing, setEditing] = useState<McpConnection | null>(null);
	const [removing, setRemoving] = useState<McpConnection | null>(null);

	useEffect(() => {
		void load();
		void loadAnalytics();
	}, [load, loadAnalytics]);

	return (
		<div className="mx-auto max-w-5xl space-y-6 pb-10">
			<div className="space-y-2">
				<p className="eyebrow">Model Context Protocol</p>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					Connected apps
				</h1>
				<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					AI assistants you let work with your Mizan data. You choose what each one may do, and you can take it back at any time.
				</p>
			</div>

			{error && (
				<Alert variant="destructive">
					<AlertDescription>{error}</AlertDescription>
				</Alert>
			)}

			<Tabs defaultValue="apps">
				<TabsList>
					<TabsTrigger value="apps">Connected apps</TabsTrigger>
					<TabsTrigger value="usage">Usage</TabsTrigger>
					<TabsTrigger value="add">Add an app</TabsTrigger>
				</TabsList>

				<TabsContent value="apps" className="mt-4">
					{loading ? (
						<div className="space-y-3">
							<Skeleton className="h-24" />
							<Skeleton className="h-24" />
						</div>
					) : connections.length === 0 ? (
						<div className="card p-6 text-center">
							<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">No apps connected yet</p>
							<p className="mt-1 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
								Add Mizan to Claude, ChatGPT, Gemini or another assistant. The Add an app tab shows how.
							</p>
						</div>
					) : (
						<ul className="card divide-y divide-charcoal-blue-100 dark:divide-white/10">
							{connections.map((connection) => (
								<ConnectionRow
									key={connection.id}
									connection={connection}
									summary={summarizeAccess(groups, connection.scopes ?? [])}
									onEdit={() => setEditing(connection)}
									onDisconnect={() => setRemoving(connection)}
								/>
							))}
						</ul>
					)}
				</TabsContent>

				<TabsContent value="usage" className="mt-4 space-y-4">
					{analyticsLoading ? (
						<div className="grid gap-4 md:grid-cols-3">
							<Skeleton className="h-28" />
							<Skeleton className="h-28" />
							<Skeleton className="h-28" />
						</div>
					) : !analytics || (analytics.overview?.totalCalls ?? 0) === 0 ? (
						<div className="card p-6 text-center text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							No activity in the last 30 days. Calls from your connected apps appear here.
						</div>
					) : (
						<UsagePanel analytics={analytics} />
					)}
				</TabsContent>

				<TabsContent value="add" className="mt-4">
					<AddAppGuide />
				</TabsContent>
			</Tabs>

			{editing && (
				<EditAccessDialog
					connection={editing}
					groups={groups}
					onClose={() => setEditing(null)}
					onSave={async (change) => {
						const saved = await update(editing.id!, change);
						if (saved) setEditing(null);
					}}
				/>
			)}

			<ConfirmationModal
				isOpen={removing !== null}
				onClose={() => setRemoving(null)}
				onConfirm={async () => {
					if (removing && (await disconnect(removing.id!))) {
						appToast.success(`${removing.clientName} disconnected`);
						setRemoving(null);
					}
				}}
				title={`Disconnect ${removing?.clientName ?? "this app"}?`}
				message="It loses access on its next request. You can connect it again later."
				confirmText="Disconnect"
				isDanger
			/>
		</div>
	);
}

function ConnectionRow({
	connection,
	summary,
	onEdit,
	onDisconnect,
}: {
	connection: McpConnection;
	summary: string[];
	onEdit: () => void;
	onDisconnect: () => void;
}) {
	const names = (connection.households ?? []).map((h) => h.name ?? "");

	return (
		<li className="space-y-3 p-4 sm:flex sm:items-start sm:justify-between sm:gap-6 sm:space-y-0">
			<div className="min-w-0 space-y-1">
				<p className="flex flex-wrap items-center gap-2 font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">
					{connection.clientName}
					{connection.verifiedHost && (
						<span className="inline-flex items-center gap-1 text-xs font-normal text-charcoal-blue-500 dark:text-charcoal-blue-400">
							<Icon name="circleCheck" size={14} /> {connection.verifiedHost}
						</span>
					)}
					{connection.source === "dynamic" && (
						<span className="text-xs font-normal text-charcoal-blue-500 dark:text-charcoal-blue-400">Unverified</span>
					)}
				</p>
				<p className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
					{summary.length > 0 ? summary.join(" · ") : "No permissions"}
				</p>
				<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{householdSummary(connection.householdMode ?? "none", names)} · Last used {formatWhen(connection.lastUsedAt)} ·{" "}
					<span className="tabular-nums">{connection.calls30Days ?? 0}</span> calls in 30 days
					{(connection.failed30Days ?? 0) > 0 && (
						<>
							, <span className="tabular-nums">{connection.failed30Days}</span> failed
						</>
					)}
				</p>
			</div>
			<div className="flex shrink-0 gap-2">
				{!connection.isFirstParty && (
					<button type="button" className="btn-secondary" onClick={onEdit}>
						Edit access
					</button>
				)}
				<button type="button" className="btn-secondary text-destructive" onClick={onDisconnect}>
					Disconnect
				</button>
			</div>
		</li>
	);
}

type Analytics = NonNullable<ReturnType<typeof useMcpAnalytics>["analytics"]>;

function UsagePanel({ analytics }: { analytics: Analytics }) {
	const overview = analytics.overview!;
	const apps = analytics.clientUsage ?? [];
	const tools = analytics.toolUsage ?? [];
	const days = analytics.dailyUsage ?? [];
	const busiest = Math.max(1, ...days.map((d) => d.callCount ?? 0));

	return (
		<>
			<dl className="grid gap-4 sm:grid-cols-3">
				<Metric label="Calls" value={String(overview.totalCalls ?? 0)} note={`${(overview.successRate ?? 0).toFixed(1)}% succeeded`} />
				<Metric label="Average time" value={`${overview.averageExecutionTimeMs ?? 0} ms`} note="Per call" />
				<Metric label="Apps used" value={String(overview.uniqueClientsUsed ?? 0)} note="Last 30 days" />
			</dl>

			<div className="card p-4">
				<h2 className="mb-3 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">Calls per day</h2>
				<div className="flex h-24 items-end gap-1" role="img" aria-label="Calls per day for the last 30 days">
					{days.map((day) => (
						<div
							key={day.date}
							title={`${day.date}: ${day.callCount} calls`}
							className="min-w-[3px] flex-1 bg-brand-600/70"
							style={{ height: `${Math.max(4, ((day.callCount ?? 0) / busiest) * 100)}%` }}
						/>
					))}
				</div>
			</div>

			<div className="grid gap-4 lg:grid-cols-2">
				<div className="card p-4">
					<h2 className="mb-3 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">By app</h2>
					<ul className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
						{apps.map((app) => (
							<li key={app.grantId} className="flex items-center justify-between py-3">
								<div>
									<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">{app.clientName}</p>
									<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
										Last used {formatWhen(app.lastUsed)}
									</p>
								</div>
								<p className="text-sm tabular-nums text-charcoal-blue-700 dark:text-charcoal-blue-300">
									{app.callCount} calls{(app.failureCount ?? 0) > 0 ? `, ${app.failureCount} failed` : ""}
								</p>
							</li>
						))}
					</ul>
				</div>

				<div className="card p-4">
					<h2 className="mb-3 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">By tool</h2>
					<ul className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
						{tools.slice(0, 12).map((tool) => (
							<li key={tool.toolName} className="flex items-center justify-between py-3">
								<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">{tool.toolName}</p>
								<p className="text-sm tabular-nums text-charcoal-blue-700 dark:text-charcoal-blue-300">
									{tool.callCount} calls · {tool.averageExecutionTimeMs} ms
								</p>
							</li>
						))}
					</ul>
				</div>
			</div>
		</>
	);
}

function Metric({ label, value, note }: { label: string; value: string; note: string }) {
	return (
		<div className="card p-4">
			<dt className="text-xs font-semibold uppercase text-charcoal-blue-500 dark:text-charcoal-blue-400">{label}</dt>
			<dd className="text-3xl font-semibold tracking-tight tabular-nums text-charcoal-blue-900 dark:text-charcoal-blue-50">{value}</dd>
			<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">{note}</p>
		</div>
	);
}
