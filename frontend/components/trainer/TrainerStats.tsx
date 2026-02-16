"use client";

import { useEffect, useState } from "react";
import { clientApi } from "@/lib/api.client";

interface StatsData {
	activeClients: number;
	pendingRequests: number;
	messagesThisWeek: number;
	clientGoalProgress: number;
}

export function TrainerStats() {
	const [stats, setStats] = useState<StatsData | null>(null);
	const [loading, setLoading] = useState(true);

	useEffect(() => {
		async function fetchStats() {
			try {
				const data = await clientApi<StatsData>("/api/trainer/stats");
				setStats(data);
			} catch (error) {
				console.error("Failed to fetch trainer stats:", error);
				// Use default values for demo
				setStats({
					activeClients: 12,
					pendingRequests: 3,
					messagesThisWeek: 47,
					clientGoalProgress: 78,
				});
			} finally {
				setLoading(false);
			}
		}

		fetchStats();
	}, []);

	if (loading) {
		return (
			<>
				{[1, 2, 3, 4].map((i) => (
					<div
						key={i}
						className="bg-white dark:bg-slate-900 rounded-lg shadow-md p-6 animate-pulse"
					>
						<div className="h-4 bg-gray-200 dark:bg-slate-700 rounded w-1/2 mb-2"></div>
						<div className="h-8 bg-gray-200 dark:bg-slate-700 rounded w-1/4"></div>
					</div>
				))}
			</>
		);
	}

	const statCards = [
		{
			label: "Active Clients",
			value: stats?.activeClients ?? 0,
			icon: "ri-user-heart-line",
			color: "text-green-600",
			bgColor: "bg-green-50",
		},
		{
			label: "Pending Requests",
			value: stats?.pendingRequests ?? 0,
			icon: "ri-user-add-line",
			color: "text-yellow-600",
			bgColor: "bg-yellow-50",
		},
		{
			label: "Messages This Week",
			value: stats?.messagesThisWeek ?? 0,
			icon: "ri-chat-3-line",
			color: "text-blue-600",
			bgColor: "bg-blue-50",
		},
		{
			label: "Client Goal Progress",
			value: `${stats?.clientGoalProgress ?? 0}%`,
			icon: "ri-bar-chart-line",
			color: "text-purple-600",
			bgColor: "bg-purple-50",
		},
	];

	return (
		<>
			{statCards.map((stat, index) => (
				<div key={index} className="bg-white dark:bg-slate-900 rounded-lg shadow-md p-6">
					<div className="flex items-center justify-between">
						<div>
							<p className="text-gray-500 dark:text-gray-400 text-sm">{stat.label}</p>
							<p className="text-3xl font-bold mt-1">{stat.value}</p>
						</div>
						<div className={`${stat.bgColor} p-3 rounded-full`}>
							<i className={`${stat.icon} ${stat.color} text-2xl`}></i>
						</div>
					</div>
				</div>
			))}
		</>
	);
}
