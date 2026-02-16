"use client";

import { useEffect, useState } from "react";
import { clientApi } from "@/lib/api.client";
import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ArrowLeft, MessageSquare } from "lucide-react";

interface ClientRelationship {
	relationshipId: string;
	clientId: string;
	clientName?: string;
	clientEmail?: string;
	status: string;
	canViewNutrition: boolean;
	canViewWorkouts: boolean;
	canViewMeasurements: boolean;
	canMessage: boolean;
	startedAt: string;
	endedAt?: string;
}

interface ClientHeaderProps {
	clientId: string;
}

export function ClientHeader({ clientId }: ClientHeaderProps) {
	const [relationship, setRelationship] = useState<ClientRelationship | null>(
		null
	);
	const [loading, setLoading] = useState(true);

	useEffect(() => {
		async function fetchClientRelationship() {
			try {
				const clients = await clientApi<ClientRelationship[]>(
					"/api/Trainers/clients"
				);
				const client = Array.isArray(clients) ? clients.find((c) => c.clientId === clientId) : undefined;
				setRelationship(client || null);
			} catch (error) {
				console.error("Failed to fetch client relationship:", error);
			} finally {
				setLoading(false);
			}
		}

		fetchClientRelationship();
	}, [clientId]);

	if (loading) {
		return (
			<div className="animate-pulse">
				<div className="h-8 bg-gray-200 dark:bg-slate-700 rounded w-1/4 mb-4"></div>
				<div className="h-12 bg-gray-200 dark:bg-slate-700 rounded w-1/3"></div>
			</div>
		);
	}

	if (!relationship) {
		return (
			<div>
				<Link
					href="/trainer"
					className="inline-flex items-center text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100 mb-4"
				>
					<ArrowLeft className="w-4 h-4 mr-2" />
					Back to Dashboard
				</Link>
				<div className="bg-yellow-50 dark:bg-yellow-950 border border-yellow-200 dark:border-yellow-800 rounded-lg p-4">
					<p className="text-yellow-800 dark:text-yellow-200">
						Client not found or no active relationship
					</p>
				</div>
			</div>
		);
	}

	return (
		<div>
			<Link
				href="/trainer"
				className="inline-flex items-center text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100 mb-4"
			>
				<ArrowLeft className="w-4 h-4 mr-2" />
				Back to Dashboard
			</Link>

			<div className="flex items-start justify-between">
				<div>
					<h1 className="text-3xl font-bold mb-2">
						{relationship.clientName || relationship.clientEmail || "Unknown Client"}
					</h1>
					{relationship.clientEmail && (
						<p className="text-gray-600 dark:text-gray-400 mb-4">{relationship.clientEmail}</p>
					)}

					<div className="flex flex-wrap gap-2">
						<Badge variant={relationship.status === "active" ? "default" : "secondary"}>
							{relationship.status}
						</Badge>

						{relationship.canViewNutrition && (
							<Badge variant="outline">Can View Nutrition</Badge>
						)}
						{relationship.canViewWorkouts && (
							<Badge variant="outline">Can View Workouts</Badge>
						)}
						{relationship.canViewMeasurements && (
							<Badge variant="outline">Can View Measurements</Badge>
						)}
					</div>
				</div>

				{relationship.canMessage && (
					<Link href={`/chat?clientId=${clientId}`}>
						<Button>
							<MessageSquare className="w-4 h-4 mr-2" />
							Message Client
						</Button>
					</Link>
				)}
			</div>
		</div>
	);
}
