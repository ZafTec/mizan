import Link from "next/link";
import { notFound } from "next/navigation";
import { getAdminAchievement } from "@/data/admin/achievements";
import AchievementForm from "../../AchievementForm";

export default async function EditAchievementPage({
	params,
}: {
	params: Promise<{ id: string }>;
}) {
	const { id } = await params;
	const achievement = await getAdminAchievement(id);
	if (!achievement) notFound();

	return (
		<div className="mx-auto max-w-2xl space-y-6">
			<header className="space-y-2">
				<Link
					href="/admin/achievements"
					className="text-xs text-charcoal-blue-500 hover:text-verdigris-600 dark:text-charcoal-blue-400"
				>
					← Achievements
				</Link>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{achievement.name}
				</h1>
			</header>

			<AchievementForm
				initial={{
					id: achievement.id,
					name: achievement.name,
					description: achievement.description ?? "",
					iconUrl: achievement.iconUrl ?? "",
					points: achievement.points,
					category: achievement.category ?? "nutrition",
					criteriaType: achievement.criteriaType ?? "meals_logged",
					threshold: achievement.threshold,
				}}
			/>
		</div>
	);
}
