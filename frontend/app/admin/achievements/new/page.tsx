import Link from "next/link";
import AchievementForm, { EMPTY_DRAFT } from "../AchievementForm";

export const metadata = { title: "New achievement | Mizan admin" };

export default function NewAchievementPage() {
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
					New achievement
				</h1>
			</header>

			<AchievementForm initial={EMPTY_DRAFT} />
		</div>
	);
}
