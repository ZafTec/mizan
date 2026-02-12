"use client";

import { useSession } from "@/lib/auth-client";
import { clientApi } from "@/lib/api.client";
import Image from "next/image";
import Link from "next/link";
import { useState, useEffect } from "react";
import { toast } from "sonner";

interface TrainerPublic {
	id: string;
	name: string | null;
	email: string;
	image: string | null;
	bio: string | null;
	specialties: string | null;
	clientCount: number;
}

export default function TrainersPage() {
	const { data: session, isPending } = useSession();
	const [trainers, setTrainers] = useState<TrainerPublic[]>([]);
	const [isLoading, setIsLoading] = useState(true);
	const [searchQuery, setSearchQuery] = useState("");
	const [requestingTrainerId, setRequestingTrainerId] = useState<string | null>(null);

	useEffect(() => {
		const fetchTrainers = async () => {
			try {
				const data = await clientApi<TrainerPublic[]>("/api/Trainers/available");
				setTrainers(data);
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
			toast.success("Trainer request sent successfully!");
		} catch (error) {
			console.error("Failed to send request:", error);
			toast.error("Failed to send trainer request");
		} finally {
			setRequestingTrainerId(null);
		}
	};

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

	const filteredTrainers = trainers.filter((trainer) => {
		const query = searchQuery.toLowerCase();
		return (
			trainer.name?.toLowerCase().includes(query) ||
			trainer.email.toLowerCase().includes(query) ||
			trainer.specialties?.toLowerCase().includes(query)
		);
	});

	return (
		<div className="max-w-6xl mx-auto space-y-6">
			{/* Header */}
			<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
				<div>
					<h1 className="text-2xl font-bold text-slate-900">Find a Trainer</h1>
					<p className="text-slate-500 mt-1">
						Connect with certified trainers to achieve your goals
					</p>
				</div>
				<Link href="/profile" className="btn-secondary">
					<i className="ri-arrow-left-line" />
					Back to Profile
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
					/>
				</div>
			</div>

			{/* Trainers Grid */}
			{filteredTrainers.length === 0 ? (
				<div className="card p-12 text-center">
					<i className="ri-user-search-line text-6xl text-slate-300 mb-4" />
					<h3 className="text-lg font-semibold text-slate-900 mb-2">
						No trainers found
					</h3>
					<p className="text-slate-500">
						Try adjusting your search or check back later
					</p>
				</div>
			) : (
				<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
					{filteredTrainers.map((trainer) => (
						<div key={trainer.id} className="card p-6 hover:shadow-lg transition-shadow">
							<div className="flex flex-col items-center text-center mb-4">
								{trainer.image ? (
									<Image
										src={trainer.image}
										alt={trainer.name || trainer.email}
										width={80}
										height={80}
										className="w-20 h-20 rounded-2xl object-cover border-4 border-white shadow-lg mb-3"
									/>
								) : (
									<div className="w-20 h-20 rounded-2xl bg-gradient-to-br from-emerald-400 to-teal-600 flex items-center justify-center border-4 border-white shadow-lg mb-3">
										<span className="text-2xl font-bold text-white">
											{trainer.email.charAt(0).toUpperCase()}
										</span>
									</div>
								)}
								<h3 className="font-semibold text-slate-900 mb-1">
									{trainer.name || trainer.email.split("@")[0]}
								</h3>
								<p className="text-sm text-slate-500 mb-2">{trainer.email}</p>
								<div className="flex items-center gap-2 text-xs text-slate-600">
									<i className="ri-group-line" />
									<span>{trainer.clientCount} clients</span>
								</div>
							</div>

							{trainer.specialties && (
								<div className="mb-4">
									<p className="text-sm text-slate-700">{trainer.specialties}</p>
								</div>
							)}

							<button
								onClick={() => handleSendRequest(trainer.id)}
								disabled={requestingTrainerId === trainer.id}
								className="btn-primary w-full"
							>
								{requestingTrainerId === trainer.id ? (
									<>
										<div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white" />
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
			<div className="card p-6 border-2 border-blue-200 bg-gradient-to-br from-blue-50 to-indigo-50">
				<div className="flex items-start gap-4">
					<div className="w-12 h-12 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center flex-shrink-0">
						<i className="ri-information-line text-2xl text-white" />
					</div>
					<div>
						<h3 className="font-semibold text-slate-900 mb-2">
							How it works
						</h3>
						<ul className="text-sm text-slate-600 space-y-2">
							<li className="flex items-start gap-2">
								<i className="ri-arrow-right-s-line text-blue-500 mt-0.5" />
								<span>
									Send a connection request to your preferred trainer
								</span>
							</li>
							<li className="flex items-start gap-2">
								<i className="ri-arrow-right-s-line text-blue-500 mt-0.5" />
								<span>
									Wait for the trainer to accept and set your permissions
								</span>
							</li>
							<li className="flex items-start gap-2">
								<i className="ri-arrow-right-s-line text-blue-500 mt-0.5" />
								<span>
									Once accepted, your trainer can view your progress and send you personalized plans
								</span>
							</li>
						</ul>
					</div>
				</div>
			</div>
		</div>
	);
}
