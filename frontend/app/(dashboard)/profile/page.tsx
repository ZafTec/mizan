"use client";

import { useEffect, useMemo, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useSession } from "@/lib/auth-client";
import Loading from "@/components/Loading";
import { AnimatedIcon, type AnimatedIconName } from "@/components/ui/animated-icon";
import { getProfileObservations, type ProfileObservations } from "@/lib/api/profile";

type QuickLink = {
	href: string;
	title: string;
	description: string;
	icon: AnimatedIconName;
};

export default function ProfilePage() {
	const { data: session, isPending } = useSession();
	const [observations, setObservations] = useState<ProfileObservations | null>(null);
	const [loadingObservations, setLoadingObservations] = useState(true);

	useEffect(() => {
		if (!session?.user) {
			return;
		}

		let cancelled = false;

		(async () => {
			try {
				const result = await getProfileObservations();
				if (!cancelled) {
					setObservations(result);
				}
			} catch (error) {
				console.error("Failed to load profile observations:", error);
			} finally {
				if (!cancelled) {
					setLoadingObservations(false);
				}
			}
		})();

		return () => {
			cancelled = true;
		};
	}, [session?.user]);

	const quickLinks = useMemo<QuickLink[]>(() => {
		const links: QuickLink[] = [
			// {
			// 	href: "/profile/settings",
			// 	title: "Settings center",
			// 	description: "Account, appearance, export, sessions, and delete flow.",
			// 	icon: "sparkles",
			// },
			{
				href: "/goal",
				title: "Nutrition goals",
				description: "Set and update your daily macro targets.",
				icon: "chartLine",
			},
			{
				href: "/meals",
				title: "Food diary",
				description: "View your logged meals and daily totals.",
				icon: "flame",
			},
			{
				href: "/workouts",
				title: "Workouts",
				description: "Log sessions and browse exercises.",
				icon: "rocket",
			},
			{
				href: "/body-measurements",
				title: "Measurements",
				description: "Track your body composition over time.",
				icon: "trendingUp",
			},
			{
				href: "/achievements",
				title: "Achievements",
				description: "Your streaks, badges, and milestones.",
				icon: "circleCheck",
			},
		];

		if (session?.user) {
			links.push({
				href: "/profile/mcp",
				title: "MCP tools",
				description: "Developer tokens and usage.",
				icon: "bot",
			});
		}

		return links;
	}, [session?.user]);

	if (isPending) {
		return (
			<div className="flex min-h-[60vh] items-center justify-center">
				<Loading />
			</div>
		);
	}

	if (!session?.user) {
		return (
			<div className="flex min-h-[60vh] items-center justify-center">
				<p className="text-charcoal-blue-500">Not authenticated</p>
			</div>
		);
	}

	const user = session.user;
	const isTrainer = user.role === "trainer" || user.role === "admin";
	const showRoleCard = Boolean(user.role && user.role !== "user");

	return (
		<div className="mx-auto max-w-6xl space-y-6" data-testid="profile-page">
			<section className="surface-panel p-6 sm:p-8">
				<div className="flex items-start gap-4 sm:gap-5">
					<div className="flex items-start gap-4 sm:gap-5">
						<ProfileAvatar image={user.image} email={user.email} name={user.name} />
						<div>
							<p className="eyebrow mb-3">
								<AnimatedIcon name="user" size={14} aria-hidden="true" />
								Profile hub
							</p>
							<h1 className="text-3xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{user.name || user.email}
							</h1>
							<p className="mt-1 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{user.email}</p>
						</div>
					</div>
				</div>

				<div className={`mt-6 grid gap-3 sm:grid-cols-2 ${showRoleCard ? "xl:grid-cols-4" : "xl:grid-cols-3"}`}>
					{showRoleCard ? (
						<StatCard
							label="Role"
							value={user.role || "user"}
							helper={isTrainer ? "Trainer access" : "Standard access"}
						/>
					) : null}
					<StatCard
						label="Joined"
						value={loadingObservations || !observations ? "--" : formatDate(observations.joinedAt)}
						helper="Account age"
					/>
					<StatCard
						label="Current streak"
						value={loadingObservations || !observations ? "--" : `${observations.streakCount} days`}
						helper={loadingObservations || !observations ? "Loading..." : `Longest ${observations.longestStreak} days`}
					/>
					<StatCard
						label="Active goal"
						value={loadingObservations || !observations ? "--" : observations.goalSummary}
						helper="Saved nutrition targets"
					/>
				</div>
			</section>

			<div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
				<section className="card p-6 sm:p-7">
					<div className="flex items-center justify-between gap-4">
						<div>
							<h2 className="text-xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Account hub</h2>
						</div>
						<Link href="/profile/settings" className="btn-secondary btn-sm">
							Open settings
							<AnimatedIcon name="arrowRight" size={16} aria-hidden="true" />
						</Link>
					</div>

					<div className="mt-6 grid gap-3 sm:grid-cols-2">
						{quickLinks.map((link) => (
							<Link
								key={link.href}
								href={link.href}
								className="group rounded-3xl border border-charcoal-blue-200 bg-white p-4 transition-colors hover:border-charcoal-blue-300 dark:border-white/10 dark:bg-charcoal-blue-950 dark:hover:border-white/20"
							>
								<div className="flex items-start gap-3">
									<span className="icon-chip h-11 w-11 text-brand-600 dark:text-brand-300">
										<AnimatedIcon name={link.icon} size={18} aria-hidden="true" />
									</span>
									<div className="min-w-0">
										<div className="flex items-center justify-between gap-3">
											<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">{link.title}</p>
											<AnimatedIcon name="arrowRight" size={16} aria-hidden="true" className="text-charcoal-blue-400 group-hover:text-charcoal-blue-900 dark:group-hover:text-white" />
										</div>
										<p className="mt-1 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{link.description}</p>
									</div>
								</div>
							</Link>
						))}
					</div>
				</section>

				<section className="space-y-6">
					<div className="card p-6">
						<div className="flex items-start gap-3">
							<span className="icon-chip h-11 w-11 text-brand-600 dark:text-brand-300">
								<AnimatedIcon name="activity" size={18} aria-hidden="true" />
							</span>
						<div>
							<h2 className="text-xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Current read</h2>
						</div>
						</div>

						{loadingObservations || !observations ? (
							<div className="mt-6 flex min-h-48 items-center justify-center">
								<Loading />
							</div>
						) : (
							<div className="mt-6 space-y-3">
								<InsightRow label="Meal logging cadence" value={`${observations.mealLoggingDays}/14 tracked days`} helper={`Average ${observations.averageCalories} kcal on logged days`} />
								<InsightRow label="Achievements" value={`${observations.achievementCount} earned`} helper={`${observations.totalAchievementPoints} total points`} />
								<InsightRow label="MCP usage" value={`${observations.mcpCalls} calls`} helper={`${observations.mcpSuccessRate}% success rate`} />
							</div>
						)}
					</div>

				</section>
			</div>
		</div>
	);
}

function ProfileAvatar({
	image,
	email,
	name,
}: {
	image?: string | null;
	email?: string | null;
	name?: string | null;
}) {
	if (image) {
		return (
			<div className="relative h-20 w-20 overflow-hidden rounded-[28px] ring-1 ring-brand-500/20 sm:h-24 sm:w-24">
				<Image src={image} alt={name || email || "User"} fill sizes="(max-width: 640px) 80px, 96px" className="object-cover" />
			</div>
		);
	}

	return (
		<div className="flex h-20 w-20 items-center justify-center rounded-[28px] bg-brand-600 text-2xl font-semibold text-white ring-1 ring-brand-500/20 dark:bg-brand-500 sm:h-24 sm:w-24">
			{(email || "U").charAt(0).toUpperCase()}
		</div>
	);
}

function StatCard({
	label,
	value,
	helper,
}: {
	label: string;
	value: string;
	helper: string;
}) {
	return (
		<div className="rounded-3xl border border-charcoal-blue-200 bg-white/90 p-4 dark:border-white/10 dark:bg-charcoal-blue-950/70">
			<p className="text-xs font-semibold uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">{label}</p>
			<p className="mt-3 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{value}</p>
			<p className="mt-2 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{helper}</p>
		</div>
	);
}

function InsightRow({
	label,
	value,
	helper,
}: {
	label: string;
	value: string;
	helper: string;
}) {
	return (
		<div className="rounded-3xl border border-charcoal-blue-200 bg-charcoal-blue-50/90 p-4 dark:border-white/10 dark:bg-charcoal-blue-900/70">
			<div className="flex items-center justify-between gap-3">
				<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">{label}</p>
				<p className="text-sm font-medium text-brand-700 dark:text-brand-300">{value}</p>
			</div>
			<p className="mt-2 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{helper}</p>
		</div>
	);
}

function formatDate(value?: string | null) {
	if (!value) return "--";
	return new Date(value).toLocaleDateString(undefined, {
		year: "numeric",
		month: "short",
		day: "numeric",
	});
}
