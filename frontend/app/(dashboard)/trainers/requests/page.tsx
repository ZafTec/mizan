"use client";

import { useSession } from "@/lib/auth-client";
import { clientApi } from "@/lib/api.client";
import Image from "next/image";
import Link from "next/link";
import { useState, useEffect } from "react";
import Loading from "@/components/Loading";
import { appToast } from "@/lib/toast";
import type { MyTrainerRequestDto, MyTrainerRequestPagedResultDto } from "@/types/api-contracts";
import { getPagedItems } from "@/types/api-contracts";

export default function TrainerRequestsPage() {
	const { data: session, isPending } = useSession();
	const [requests, setRequests] = useState<MyTrainerRequestDto[]>([]);
	const [isLoading, setIsLoading] = useState(true);

	useEffect(() => {
		const fetchRequests = async () => {
			try {
				const data = await clientApi<MyTrainerRequestPagedResultDto>("/api/Trainers/my-requests");
				setRequests(getPagedItems(data));
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
				<Loading />
			</div>
		);
	}

	if (!session?.user) {
		return (
			<div className="flex items-center justify-center min-h-screen">
				<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400">Not authenticated</p>
			</div>
		);
	}

	return (
		<div className="max-w-4xl mx-auto space-y-6">
			{/* Header */}
			<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
				<div>
					<h1 className="text-2xl font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">
						Trainer Requests
					</h1>
					<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 mt-1">
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
					<i className="ri-mail-send-line text-6xl text-charcoal-blue-300 dark:text-charcoal-blue-600 mb-4" />
					<h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 mb-2">
						No Pending Requests
					</h3>
					<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 mb-6">
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
						<div key={request.relationshipId || request.trainerId || request.trainerEmail || "trainer-request"} className="card p-6">
							<div className="flex flex-col sm:flex-row items-center gap-6">
								<div className="relative shrink-0">
									{request.trainerImage ? (
										<Image
											src={request.trainerImage}
											alt={request.trainerName || request.trainerEmail || "Trainer"}
											width={80}
											height={80}
											className="w-20 h-20 rounded-2xl object-cover border-4 border-white dark:border-white/10 shadow-lg"
										/>
									) : (
									<div className="w-20 h-20 rounded-2xl bg-emerald-600 flex items-center justify-center border-4 border-white dark:border-white/10 shadow-lg">
											<span className="text-2xl font-bold text-white">
												{(request.trainerEmail || "?").charAt(0).toUpperCase()}
											</span>
										</div>
									)}
								</div>

								<div className="flex-1 text-center sm:text-left">
									<h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
										{request.trainerName || request.trainerEmail?.split("@")[0]}
									</h3>
									<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400">{request.trainerEmail}</p>
									<div className="flex items-center gap-2 mt-2 justify-center sm:justify-start">
										<span className="inline-flex items-center gap-1 px-3 py-1 rounded-2xl bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-300 text-sm font-medium">
											<i className="ri-time-line" />
											Pending
										</span>
										<span className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
											Sent {request.requestedAt ? new Date(request.requestedAt).toLocaleDateString() : "Unknown date"}
										</span>
									</div>
								</div>

								<div className="flex gap-2 shrink-0">
								<button
									className="btn-secondary"
									onClick={() => {
										appToast.info("Cancel request is not available yet");
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
			<div className="card p-6 border border-blue-200 dark:border-blue-800 bg-white/70 dark:bg-charcoal-blue-950/60">
				<div className="flex items-start gap-4">
					<div className="w-12 h-12 rounded-xl bg-blue-600 flex items-center justify-center shrink-0">
						<i className="ri-information-line text-2xl text-white" />
					</div>
					<div>
						<h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 mb-2">
							What happens next?
						</h3>
						<ul className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-400 space-y-2">
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
