"use client";

import Link from "next/link";
import { AnimatedIcon, type AnimatedIconName } from "@/components/ui/animated-icon";
import { cn } from "@/lib/utils";

type Action = {
	href: string;
	label: string;
	icon: AnimatedIconName;
	tone: "brand" | "accent" | "peach" | "sun";
};

const ACTIONS: Action[] = [
	{ href: "/meals/add", label: "Log Meal", icon: "flame", tone: "peach" },
	{ href: "/workouts", label: "Start Workout", icon: "activity", tone: "brand" },
	{ href: "/body-measurements", label: "Log Weight", icon: "chartLine", tone: "sun" },
	{ href: "/recipes/add", label: "New Recipe", icon: "cookingPot", tone: "accent" },
];

const toneClass: Record<Action["tone"], string> = {
	brand:
		"text-verdigris-700 dark:text-verdigris-300 [&_.qa-icon]:bg-verdigris-500/15 [&_.qa-icon]:text-verdigris-700 dark:[&_.qa-icon]:bg-verdigris-500/20 dark:[&_.qa-icon]:text-verdigris-200",
	accent:
		"text-sandy-brown-700 dark:text-sandy-brown-300 [&_.qa-icon]:bg-sandy-brown-500/15 [&_.qa-icon]:text-sandy-brown-700 dark:[&_.qa-icon]:bg-sandy-brown-500/20 dark:[&_.qa-icon]:text-sandy-brown-200",
	peach:
		"text-burnt-peach-700 dark:text-burnt-peach-300 [&_.qa-icon]:bg-burnt-peach-500/15 [&_.qa-icon]:text-burnt-peach-700 dark:[&_.qa-icon]:bg-burnt-peach-500/20 dark:[&_.qa-icon]:text-burnt-peach-200",
	sun:
		"text-tuscan-sun-700 dark:text-tuscan-sun-300 [&_.qa-icon]:bg-tuscan-sun-500/15 [&_.qa-icon]:text-tuscan-sun-700 dark:[&_.qa-icon]:bg-tuscan-sun-500/20 dark:[&_.qa-icon]:text-tuscan-sun-200",
};

export default function QuickActions() {
	return (
		<div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
			{ACTIONS.map((a) => (
				<Link
					key={a.href}
					href={a.href}
					className={cn(
						"glass-panel group flex items-center justify-between gap-3 p-4 transition-all duration-300 hover:-translate-y-0.5 hover:shadow-[0_18px_40px_-22px_rgba(15,23,42,0.32)]",
						toneClass[a.tone]
					)}
				>
					<span className="text-sm font-semibold">{a.label}</span>
					<span className="qa-icon flex h-10 w-10 items-center justify-center rounded-2xl transition-transform group-hover:scale-110">
						<AnimatedIcon name={a.icon} size={18} />
					</span>
				</Link>
			))}
		</div>
	);
}
