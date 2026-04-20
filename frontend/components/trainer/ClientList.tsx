"use client";

import { useEffect, useState } from "react";
import { clientApi } from "@/lib/api.client";
import Link from "next/link";
import { appToast } from "@/lib/toast";
import type { TrainerClientDto, TrainerClientPagedResultDto } from "@/types/api-contracts";
import { getPagedItems } from "@/types/api-contracts";

export function ClientList() {
	const [clients, setClients] = useState<TrainerClientDto[]>([]);
	const [loading, setLoading] = useState(true);
	const [filter, setFilter] = useState<"all" | "active" | "paused">("all");

	useEffect(() => {
		async function fetchClients() {
			try {
				const data = await clientApi<TrainerClientPagedResultDto>("/api/Trainers/clients");
				setClients(getPagedItems(data));
			} catch (error) {
				console.error("Failed to fetch clients:", error);
				appToast.error(error, "Failed to load clients");
			} finally {
				setLoading(false);
			}
		}

		fetchClients();
	}, []);

	const filteredClients = clients.filter((client) => {
		if (filter === "all") return true;
		if (filter === "active") return client.status === "active";
		if (filter === "paused") return client.status === "paused";
		return true;
	});

	if (loading) {
		return (
			<div className="space-y-4">
				{[1, 2, 3].map((i) => (
					<div key={i} className="animate-pulse flex items-center space-x-4">
						<div className="w-12 h-12 bg-gray-200 dark:bg-charcoal-blue-700 rounded-2xl"></div>
						<div className="flex-1">
							<div className="h-4 bg-gray-200 dark:bg-charcoal-blue-700 rounded w-1/4 mb-2"></div>
							<div className="h-3 bg-gray-200 dark:bg-charcoal-blue-700 rounded w-1/3"></div>
						</div>
					</div>
				))}
			</div>
		);
	}

	return (
		<div>
			<div className="flex space-x-2 mb-4">
				{(["all", "active", "paused"] as const).map((f) => (
					<button
						key={f}
						onClick={() => setFilter(f)}
						className={`px-3 py-1 rounded-full text-sm capitalize ${
							filter === f
								? "bg-green-600 text-white"
								: "bg-gray-100 dark:bg-charcoal-blue-900 text-gray-600 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-charcoal-blue-700"
						}`}
					>
						{f}
					</button>
				))}
			</div>

			<div className="space-y-4">
				{filteredClients.map((client) => (
					<div
						key={client.relationshipId || client.clientId || "trainer-client"}
						className="flex items-center justify-between p-3 bg-gray-50 dark:bg-charcoal-blue-900 rounded-lg hover:bg-gray-100 dark:hover:bg-charcoal-blue-700 transition-colors"
					>
						<div className="flex items-center space-x-3">
							<div className="w-10 h-10 bg-green-100 dark:bg-green-900/30 rounded-2xl flex items-center justify-center">
								<span className="text-green-600 dark:text-green-400 font-semibold">
									{(client.clientName || client.clientEmail || "?").charAt(0)}
								</span>
							</div>
							<div>
								<p className="font-medium">
									{client.clientName || client.clientEmail || "Unknown Client"}
								</p>
								<p className="text-sm text-gray-500 dark:text-gray-400">
									Started: {client.startedAt ? new Date(client.startedAt).toLocaleDateString() : "Unknown date"}
								</p>
							</div>
						</div>

						<div className="flex items-center space-x-4">
							<div className="text-right text-xs text-gray-500 dark:text-gray-400">
								{client.canViewNutrition && <div>✓ Nutrition</div>}
								{client.canViewWorkouts && <div>✓ Workouts</div>}
							</div>

							{client.clientId ? (
								<Link
									href={`/trainer/clients/${client.clientId}`}
									className="p-2 hover:bg-gray-200 dark:hover:bg-charcoal-blue-700 rounded-full"
								>
									<i className="ri-arrow-right-line text-gray-600 dark:text-gray-300"></i>
								</Link>
							) : null}
						</div>
					</div>
				))}

				{filteredClients.length === 0 && (
					<p className="text-center text-gray-500 dark:text-gray-400 py-8">
						No clients found matching the filter.
					</p>
				)}
			</div>
		</div>
	);
}
