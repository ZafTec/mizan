"use client";

import { useSession } from "@/lib/auth-client";
import { clientApi } from "@/lib/api.client";
import Image from "next/image";
import Link from "next/link";
import { useState, useEffect } from "react";
import { appToast } from "@/lib/toast";
import Loading from "@/components/Loading";
import { useDebounce } from "@/lib/hooks/useDebounce";
import type { TrainerPublicDto, TrainerPublicPagedResultDto } from "@/types/api-contracts";
import { getPagedItems } from "@/types/api-contracts";
import { AppFeatureIllustration } from "@/components/illustrations/AppFeatureIllustration";

export default function TrainersPage() {
	const { data: session, isPending } = useSession();
	const [trainers, setTrainers] = useState<TrainerPublicDto[]>([]);
	const [isLoading, setIsLoading] = useState(true);
	const [searchQuery, setSearchQuery] = useState("");
	const [requestingTrainerId, setRequestingTrainerId] = useState<string | null>(null);

	const debouncedQuery = useDebounce(searchQuery, 200);

	useEffect(() => {
		const fetchTrainers = async () => {
			try {
				const data = await clientApi<TrainerPublicPagedResultDto>("/api/Trainers/available");
				setTrainers(getPagedItems(data));
			} catch (error) {
				console.error("Failed to fetch trainers:", error);
			} finally {
				setIsLoading(false);
			}
		};

		if (session?.user) {
			fetchTrainers();
		}
	}, [session]);

	const handleSendRequest = async (trainerId: string) => {
		setRequestingTrainerId(trainerId);
		try {
			await clientApi("/api/Trainers/request", {
				method: "POST",
				body: { trainerId },
			});
			appToast.success("Trainer request sent");
		} catch (error) {
			console.error("Failed to send request:", error);
			appToast.error(error, "Failed to send trainer request");
		} finally {
			setRequestingTrainerId(null);
		}
	};

	if (isPending || isLoading) {
		return (
			<div className="flex items-center justify-center min-h-[50vh]">
				<Loading />
			</div>
		);
	}

	if (!session?.user) {
		return (
			<div className="flex items-center justify-center min-h-[50vh]">
				<p className="text-slate-500 dark:text-slate-400">Not authenticated</p>
			</div>
		);
	}

	const filteredTrainers = trainers.filter((trainer) => {
		const query = debouncedQuery.toLowerCase();
		const trainerEmail = trainer.email?.toLowerCase() ?? "";
		const specialties = trainer.specialties?.toLowerCase() ?? "";
		return (
			trainer.name?.toLowerCase().includes(query) ||
			trainerEmail.includes(query) ||
			specialties.includes(query)
		);
	});

	return (
		<div className="max-w-6xl mx-auto space-y-6" data-testid="trainers-page">
			{/* Header */}
			<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
				<div className="flex items-center gap-4">
					<div className="hidden w-20 sm:block opacity-95">
						<AppFeatureIllustration variant="trainers" className="h-auto w-full" />
					</div>
					<div className="space-y-2">
						<p className="eyebrow">Coaching</p>
						<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
							Find a trainer
						</h1>
						<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Connect with certified coaches to hit your goals faster.
						</p>
					</div>
				</div>
				<Link href="/profile" className="btn-secondary">
					<i className="ri-arrow-left-line" />
					Back to profile
				</Link>
			</div>

			{/* Search Bar */}
			<div className="card p-4">
				<div className="relative">
					<i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
					<input
						type="text"
						placeholder="Search trainers by name, email, or specialty..."
						value={searchQuery}
						onChange={(e) => setSearchQuery(e.target.value)}
						className="input pl-10 w-full"
						data-testid="search-input"
					/>
				</div>
			</div>

			{/* Trainers Grid */}
			{filteredTrainers.length === 0 ? (
				<div className="card p-12 text-center">
					<i className="ri-user-search-line text-6xl text-slate-300 dark:text-slate-600 mb-4" />
					<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">
						No trainers found
					</h3>
					<p className="text-slate-500 dark:text-slate-400">
						Try adjusting your search or check back later
					</p>
				</div>
			) : (
				<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
					{filteredTrainers.map((trainer) => (
						<div key={trainer.id || trainer.email || "trainer-card"} className="card p-6 hover:shadow-lg transition-shadow">
							<div className="flex flex-col items-center text-center mb-4">
								{trainer.image ? (
									<Image
										src={trainer.image}
										alt={trainer.name || trainer.email || "Trainer"}
										width={80}
										height={80}
										className="w-20 h-20 rounded-2xl object-cover border-4 border-white dark:border-slate-900 shadow-lg mb-3"
									/>
								) : (
									<div className="w-20 h-20 rounded-2xl bg-emerald-600 flex items-center justify-center border-4 border-white dark:border-slate-900 shadow-lg mb-3">
										<span className="text-2xl font-bold text-white">
											{(trainer.email || "?").charAt(0).toUpperCase()}
										</span>
									</div>
								)}
								<h3 className="font-semibold text-slate-900 dark:text-slate-100 mb-1">
									{trainer.name || trainer.email?.split("@")[0] || "Unknown trainer"}
								</h3>
								<p className="text-sm text-slate-500 dark:text-slate-400 mb-2">{trainer.email}</p>
								<div className="flex items-center gap-2 text-xs text-slate-600 dark:text-slate-400">
									<i className="ri-group-line" />
									<span>{trainer.clientCount || 0} clients</span>
								</div>
							</div>

							{trainer.specialties && (
								<div className="mb-4">
									<p className="text-sm text-slate-700 dark:text-slate-300">{trainer.specialties}</p>
								</div>
							)}

							<button
								onClick={() => trainer.id && handleSendRequest(trainer.id)}
								disabled={requestingTrainerId === trainer.id || !trainer.id}
								className="btn-primary w-full"
							>
								{requestingTrainerId === trainer.id ? (
									<>
										<Loading size="sm" />
										Sending...
									</>
								) : (
									<>
										<i className="ri-user-add-line" />
										Send Request
									</>
								)}
							</button>
						</div>
					))}
				</div>
			)}

			{/* Info Card */}
			<div className="card p-6 border border-blue-200 dark:border-blue-800 bg-slate-50/90 dark:bg-slate-900/60">
				<div className="flex items-start gap-4">
					<div className="w-12 h-12 rounded-xl bg-blue-600 flex items-center justify-center shrink-0">
						<i className="ri-information-line text-2xl text-white" />
					</div>
					<div>
						<h3 className="font-semibold text-slate-900 dark:text-slate-100 mb-2">
							How it works
						</h3>
						<ul className="text-sm text-slate-600 dark:text-slate-400 space-y-2">
							<li className="flex items-start gap-2">
								<i className="ri-arrow-right-s-line text-blue-500 mt-0.5" />
								<span>Send a connection request to your preferred trainer</span>
							</li>
							<li className="flex items-start gap-2">
								<i className="ri-arrow-right-s-line text-blue-500 mt-0.5" />
								<span>Wait for the trainer to accept and set your permissions</span>
							</li>
							<li className="flex items-start gap-2">
								<i className="ri-arrow-right-s-line text-blue-500 mt-0.5" />
								<span>Once accepted, your trainer can view your progress and send you personalized plans</span>
							</li>
						</ul>
					</div>
				</div>
			</div>
		</div>
	);
}
