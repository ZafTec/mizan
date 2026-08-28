import Link from "next/link";
import Image from "next/image";
import { getMyTrainer } from "@/data/trainer";
import { Icon } from "@/components/ui/icon";

/**
 * Tier 2 - docs/REFOCUS.md §3. Absent until a coaching relationship exists.
 *
 * It states what the trainer can see, because the client controls those grants
 * (§11) and a consent decision the user cannot see is not a consent decision.
 */
export default async function TrainerStrip() {
	const trainer = await getMyTrainer();
	if (!trainer || trainer.status.toLowerCase() !== "active") return null;

	const shared = [
		trainer.canViewNutrition && "meals",
		trainer.canViewWorkouts && "workouts",
		trainer.canViewMeasurements && "measurements",
	].filter(Boolean) as string[];

	const name = trainer.trainerName || trainer.trainerEmail || "Your trainer";

	return (
		<section className="flex flex-wrap items-center gap-3 rounded-3xl border border-charcoal-blue-200 bg-white p-4 dark:border-white/10 dark:bg-charcoal-blue-900">
			{trainer.trainerImage ? (
				<div className="relative h-10 w-10 shrink-0 overflow-hidden rounded-2xl ring-1 ring-brand-500/20">
					<Image src={trainer.trainerImage} alt={name} fill sizes="40px" className="object-cover" />
				</div>
			) : (
				<span className="icon-chip h-10 w-10 shrink-0 text-brand-700 dark:text-brand-300">
					<Icon name="heart" size={18} />
				</span>
			)}

			<div className="min-w-0 flex-1">
				<p className="truncate text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					Coached by {name}
				</p>
				<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{shared.length > 0
						? `Can see your ${shared.join(", ")}`
						: "Cannot see any of your data yet"}
				</p>
			</div>

			<div className="flex shrink-0 items-center gap-2">
				{trainer.canMessage && (
					<Link href="/messaging" className="btn-ghost !rounded-2xl !py-2 text-xs">
						<Icon name="messageCircle" size={14} />
						Message
					</Link>
				)}
				<Link href="/trainers/my-trainer" className="btn-secondary !rounded-2xl !py-2 text-xs">
					Manage access
				</Link>
			</div>
		</section>
	);
}
