import Image from "next/image";
import { cn } from "@/lib/utils";

export type IllustrationVariant =
	| "achievements"
	| "dashboard"
	| "meal-plan"
	| "meal-plan-empty"
	| "progress"
	| "recipes"
	| "recipe-header"
	| "shopping"
	| "trainers"
	| "workouts";

// 1:1 mapping to /public/assets/*.svg — the assets are the source of truth,
// inline SVG scenes were removed so every page uses the same illustration set.
const ASSET_MAP: Record<IllustrationVariant, { src: string; alt: string }> = {
	dashboard: { src: "/assets/dashboard-overview.svg", alt: "Dashboard overview" },
	progress: { src: "/assets/progress-chart.svg", alt: "Progress chart" },
	"meal-plan": { src: "/assets/mean_planner.svg", alt: "Meal planner" },
	"meal-plan-empty": { src: "/assets/meal-planner-empty.svg", alt: "Empty meal planner" },
	shopping: { src: "/assets/shopping-list.svg", alt: "Shopping list" },
	recipes: { src: "/assets/recipe-book.svg", alt: "Recipe book" },
	"recipe-header": { src: "/assets/recipe_header.svg", alt: "Recipe header" },
	workouts: { src: "/assets/workout.svg", alt: "Workouts" },
	trainers: { src: "/assets/trainer_collaboration.svg", alt: "Trainer collaboration" },
	achievements: { src: "/assets/gamification-badge.svg", alt: "Achievements" },
};

export function AppFeatureIllustration({
	variant = "dashboard",
	className,
	priority = false,
}: {
	variant?: IllustrationVariant;
	className?: string;
	priority?: boolean;
}) {
	const asset = ASSET_MAP[variant];

	return (
		<Image
			src={asset.src}
			alt={asset.alt}
			width={640}
			height={420}
			priority={priority}
			className={cn("h-auto w-full select-none", className)}
		/>
	);
}
