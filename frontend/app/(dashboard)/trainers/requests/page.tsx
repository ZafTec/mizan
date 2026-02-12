"use client";

import { useSession } from "@/lib/auth-client";
import { clientApi } from "@/lib/api.client";
import Image from "next/image";
import Link from "next/link";
import { useState, useEffect } from "react";
import { toast } from "sonner";

interface TrainerRequest {
	relationshipId: string;
	trainerId: string;
	trainerName: string | null;
	trainerEmail: string | null;
	trainerImage: string | null;
	status: string;
	requestedAt: string;
}

export default function TrainerRequestsPage() {
	const { data: session, isPending } = useSession();
	const [requests, setRequests] = useState<TrainerRequest[]>([]);
	const [isLoading, setIsLoading] = useState(true);

	useEffect(() => {
		const fetchRequests = async () => {
			try {
				const data = await clientApi<TrainerRequest[]>("/api/Trainers/my-requests");
				setRequests(data);
			} catch (error) {
				console.error("Failed to fetch requests:", error);
			} finally {
				setIsLoading(false);
			}
		};

		if (session?.user) {
			fetchRequests();
		}
	}, [session]);

	if (isPending || isLoading) {
		return (
			<div className="flex items-center justify-center min-h-screen">
				<div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-500"></div>
			</div>
		);
	}

	if (!session?.user) {
		return (
			<div className="flex items-center justify-center min-h-screen">
				<p className="text-slate-500">Not authenticated</p>
			</div>
		);
	}

	return (
		<div className="max-w-4xl mx-auto space-y-6">
			{/* Header */}
			<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
				<div>
					<h1 className="text-2xl font-bold text-slate-900">
						Trainer Requests
					</h1>
					<p className="text-slate-500 mt-1">
						View your pending trainer connection requests
					</p>
				</div>
				<Link href="/profile" className="btn-secondary">
					<i className="ri-arrow-left-line" />
					Back to Profile
				</Link>
			</div>

			{/* Requests List */}
			{requests.length === 0 ? (
				<div className="card p-12 text-center">
					<i className="ri-mail-send-line text-6xl text-slate-300 mb-4" />
					<h3 className="text-lg font-semibold text-slate-900 mb-2">
						No Pending Requests
					</h3>
					<p className="text-slate-500 mb-6">
						You don't have any pending trainer connection requests
					</p>
					<Link href="/trainers" className="btn-primary inline-flex items-center gap-2">
						<i className="ri-search-line" />
						Find a Trainer
					</Link>
				</div>
			) : (
				<div className="space-y-4">
					{requests.map((request) => (
						<div key={request.relationshipId} className="card p-6">
							<div className="flex flex-col sm:flex-row items-center gap-6">
								<div className="relative flex-shrink-0">
									{request.trainerImage ? (
										<Image
											src={request.trainerImage}
											alt={request.trainerName || request.trainerEmail || "Trainer"}
											width={80}
											height={80}
											className="w-20 h-20 rounded-2xl object-cover border-4 border-white shadow-lg"
										/>
									) : (
										<div className="w-20 h-20 rounded-2xl bg-gradient-to-br from-emerald-400 to-teal-600 flex items-center justify-center border-4 border-white shadow-lg">
											<span className="text-2xl font-bold text-white">
												{request.trainerEmail?.charAt(0).toUpperCase()}
											</span>
										</div>
									)}
								</div>

								<div className="flex-1 text-center sm:text-left">
									<h3 className="text-lg font-semibold text-slate-900">
										{request.trainerName || request.trainerEmail?.split("@")[0]}
									</h3>
									<p className="text-slate-500">{request.trainerEmail}</p>
									<div className="flex items-center gap-2 mt-2 justify-center sm:justify-start">
										<span className="inline-flex items-center gap-1 px-3 py-1 rounded-full bg-amber-100 text-amber-700 text-sm font-medium">
											<i className="ri-time-line" />
											Pending
										</span>
										<span className="text-sm text-slate-500">
											Sent {new Date(request.requestedAt).toLocaleDateString()}
										</span>
									</div>
								</div>

								<div className="flex gap-2 flex-shrink-0">
									<button
										className="btn-secondary"
										onClick={() => {
											toast.info("Cancel request functionality coming soon");
										}}
									>
										<i className="ri-close-line" />
										Cancel
									</button>
								</div>
							</div>
						</div>
					))}
				</div>
			)}

			{/* Info Card */}
			<div className="card p-6 border-2 border-blue-200 bg-gradient-to-br from-blue-50 to-indigo-50">
				<div className="flex items-start gap-4">
					<div className="w-12 h-12 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center flex-shrink-0">
						<i className="ri-information-line text-2xl text-white" />
					</div>
					<div>
						<h3 className="font-semibold text-slate-900 mb-2">
							What happens next?
						</h3>
						<ul className="text-sm text-slate-600 space-y-2">
							<li className="flex items-start gap-2">
								<i className="ri-arrow-right-s-line text-blue-500 mt-0.5" />
								<span>
									The trainer will review your request and decide whether to accept
								</span>
							</li>
							<li className="flex items-start gap-2">
								<i className="ri-arrow-right-s-line text-blue-500 mt-0.5" />
								<span>
									If accepted, they will set what data they can access (nutrition, workouts, etc.)
								</span>
							</li>
							<li className="flex items-start gap-2">
								<i className="ri-arrow-right-s-line text-blue-500 mt-0.5" />
								<span>
									You'll be notified when the trainer responds to your request
								</span>
							</li>
						</ul>
					</div>
				</div>
			</div>
		</div>
	);
}
