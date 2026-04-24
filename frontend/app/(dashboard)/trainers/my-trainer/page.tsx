"use client";

import { useSession } from "@/lib/auth-client";
import { clientApi } from "@/lib/api.client";
import Image from "next/image";
import Link from "next/link";
import { useState, useEffect } from "react";
import Loading from "@/components/Loading";
import { ApiError } from "@/lib/api";

interface MyTrainerDto {
	relationshipId: string;
	trainerId: string;
	trainerName: string | null;
	trainerEmail: string | null;
	trainerImage: string | null;
	status: string;
	canViewNutrition: boolean;
	canViewWorkouts: boolean;
	canViewMeasurements: boolean;
	canMessage: boolean;
	startedAt: string;
	endedAt: string | null;
}

export default function MyTrainerPage() {
	const { data: session, isPending } = useSession();
	const [trainer, setTrainer] = useState<MyTrainerDto | null>(null);
	const [isLoading, setIsLoading] = useState(true);

	useEffect(() => {
		const fetchMyTrainer = async () => {
			try {
				const data = await clientApi<MyTrainerDto>("/api/Trainers/my-trainer");
				setTrainer(data);
			} catch (error) {
				if (error instanceof ApiError && error.status === 404) {
					setTrainer(null);
				} else {
					console.error("Failed to fetch trainer:", error);
				}
			} finally {
				setIsLoading(false);
			}
		};

		if (session?.user) {
			fetchMyTrainer();
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

	if (!trainer) {
		return (
			<div className="max-w-3xl mx-auto space-y-6">
				<div className="flex items-center justify-between">
					<div>
						<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">My Trainer</h1>
						<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 mt-1">No active trainer relationship</p>
					</div>
					<Link href="/profile" className="btn-secondary">
						<i className="ri-arrow-left-line" />
						Back to Profile
					</Link>
				</div>

				<div className="card p-12 text-center">
					<i className="ri-user-heart-line text-6xl text-charcoal-blue-300 mb-4" />
					<h3 className="text-lg font-semibold text-charcoal-blue-900 mb-2">
						No Active Trainer
					</h3>
					<p className="text-charcoal-blue-500 mb-6">
						No active trainer relationship.
					</p>
					<Link href="/trainers" className="btn-primary inline-flex items-center gap-2">
						<i className="ri-search-line" />
						Find a trainer
					</Link>
				</div>
			</div>
		);
	}

	return (
		<div className="max-w-3xl mx-auto space-y-6">
			{/* Header */}
			<div className="flex items-center justify-between">
				<div>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900">My Trainer</h1>
				</div>
				<Link href="/profile" className="btn-secondary">
					<i className="ri-arrow-left-line" />
					Back to Profile
				</Link>
			</div>

			{/* Trainer Card */}
			<div className="card p-6">
				<div className="flex flex-col sm:flex-row items-center gap-6 mb-6">
					<div className="relative">
						{trainer.trainerImage ? (
							<Image
								src={trainer.trainerImage}
								alt={trainer.trainerName || trainer.trainerEmail || "Trainer"}
								width={96}
								height={96}
								className="w-24 h-24 rounded-2xl object-cover border-4 border-white shadow-lg"
							/>
						) : (
							<div className="w-24 h-24 rounded-2xl bg-emerald-600 flex items-center justify-center border-4 border-white shadow-lg">
								<span className="text-3xl font-bold text-white">
									{trainer.trainerEmail?.charAt(0).toUpperCase()}
								</span>
							</div>
						)}
					</div>
					<div className="text-center sm:text-left flex-1">
						<h2 className="text-xl font-bold text-charcoal-blue-900">
							{trainer.trainerName || trainer.trainerEmail?.split("@")[0]}
						</h2>
						<p className="text-charcoal-blue-500">{trainer.trainerEmail}</p>
						<div className="flex items-center gap-2 mt-2 justify-center sm:justify-start">
							<span className="inline-flex items-center gap-1 px-3 py-1 rounded-full bg-emerald-100 text-emerald-700 text-sm font-medium">
								<i className="ri-checkbox-circle-line" />
								Active
							</span>
							<span className="text-sm text-charcoal-blue-500">
								Since {trainer.startedAt ? new Date(trainer.startedAt).toLocaleDateString() : "Unknown date"}
							</span>
						</div>
					</div>
				</div>

				{/* Permissions */}
				<div className="border-t border-charcoal-blue-100 pt-6">
					<h3 className="font-semibold text-charcoal-blue-900 mb-4">
						Access
					</h3>
					<div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
						<div className="flex items-center gap-3 p-3 rounded-xl bg-charcoal-blue-50">
							<div className={`w-10 h-10 rounded-xl flex items-center justify-center ${
								trainer.canViewNutrition
									? "bg-emerald-100 text-emerald-600"
									: "bg-charcoal-blue-200 text-charcoal-blue-400"
							}`}>
								<i className="ri-restaurant-2-line text-lg" />
							</div>
							<div>
								<p className="font-medium text-charcoal-blue-900">Nutrition Data</p>
								<p className="text-xs text-charcoal-blue-500">
									{trainer.canViewNutrition ? "Allowed" : "Not allowed"}
								</p>
							</div>
						</div>

						<div className="flex items-center gap-3 p-3 rounded-xl bg-charcoal-blue-50">
							<div className={`w-10 h-10 rounded-xl flex items-center justify-center ${
								trainer.canViewWorkouts
									? "bg-emerald-100 text-emerald-600"
									: "bg-charcoal-blue-200 text-charcoal-blue-400"
							}`}>
								<i className="ri-run-line text-lg" />
							</div>
							<div>
								<p className="font-medium text-charcoal-blue-900">Workout Data</p>
								<p className="text-xs text-charcoal-blue-500">
									{trainer.canViewWorkouts ? "Allowed" : "Not allowed"}
								</p>
							</div>
						</div>

						<div className="flex items-center gap-3 p-3 rounded-xl bg-charcoal-blue-50">
							<div className={`w-10 h-10 rounded-xl flex items-center justify-center ${
								trainer.canViewMeasurements
									? "bg-emerald-100 text-emerald-600"
									: "bg-charcoal-blue-200 text-charcoal-blue-400"
							}`}>
								<i className="ri-ruler-line text-lg" />
							</div>
							<div>
								<p className="font-medium text-charcoal-blue-900">Body Measurements</p>
								<p className="text-xs text-charcoal-blue-500">
									{trainer.canViewMeasurements ? "Allowed" : "Not allowed"}
								</p>
							</div>
						</div>

						<div className="flex items-center gap-3 p-3 rounded-xl bg-charcoal-blue-50">
							<div className={`w-10 h-10 rounded-xl flex items-center justify-center ${
								trainer.canMessage
									? "bg-emerald-100 text-emerald-600"
									: "bg-charcoal-blue-200 text-charcoal-blue-400"
							}`}>
								<i className="ri-message-3-line text-lg" />
							</div>
							<div>
								<p className="font-medium text-charcoal-blue-900">Direct Messaging</p>
								<p className="text-xs text-charcoal-blue-500">
									{trainer.canMessage ? "Allowed" : "Not allowed"}
								</p>
							</div>
						</div>
					</div>
				</div>
			</div>

			{/* Actions */}
			<div className="card p-6">
				<div className="space-y-3">
					{trainer.canMessage && (
						trainer.trainerId ? (
							<Link
								href={`/chat?recipientId=${trainer.trainerId}`}
								className="btn-primary w-full"
							>
								<i className="ri-message-3-line" />
								Send Message
							</Link>
						) : null
					)}
					<button className="btn-secondary w-full" disabled>
						<i className="ri-close-circle-line" />
						End Relationship (Coming Soon)
					</button>
				</div>
			</div>
		</div>
	);
}
