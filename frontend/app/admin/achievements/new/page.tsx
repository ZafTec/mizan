import Link from "next/link";
import AchievementForm from "../AchievementForm";
import { createAchievementAction } from "@/data/admin/achievement";

export const dynamic = "force-dynamic";

export const metadata = {
    title: "New achievement | Mizan admin",
};

export default function NewAchievementPage() {
    return (
        <div className="max-w-3xl mx-auto space-y-6">
            <div className="flex items-center gap-4">
                <Link
                    href="/admin/achievements"
                    className="w-10 h-10 rounded-xl bg-charcoal-blue-100 hover:bg-charcoal-blue-200 dark:bg-charcoal-blue-900 dark:hover:bg-charcoal-blue-800 flex items-center justify-center transition-colors"
                >
                    <i className="ri-arrow-left-line text-xl text-charcoal-blue-600 dark:text-charcoal-blue-300" />
                </Link>
                <div>
                    <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
                        New achievement
                    </h1>
                    <p className="text-charcoal-blue-500 dark:text-charcoal-blue-400">
                        Define the name, points and auto-unlock criteria.
                    </p>
                </div>
            </div>

            <AchievementForm
                mode="create"
                action={createAchievementAction}
                submitLabel="Create achievement"
            />
        </div>
    );
}
