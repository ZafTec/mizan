import Link from "next/link";
import { notFound } from "next/navigation";
import AchievementForm from "../../AchievementForm";
import { getAchievementById, updateAchievementAction } from "@/data/admin/achievement";
import type { FormState } from "@/helper/FormErrorHandler";

export const dynamic = "force-dynamic";

export const metadata = {
    title: "Edit achievement | Mizan admin",
};

export default async function EditAchievementPage({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id } = await params;
    const achievement = await getAchievementById(id);
    if (!achievement) notFound();

    async function action(state: FormState, formData: FormData) {
        "use server";
        return updateAchievementAction(id, state, formData);
    }

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
                        Edit {achievement.name}
                    </h1>
                    <p className="text-charcoal-blue-500 dark:text-charcoal-blue-400">
                        Changes take effect on the next user activity.
                    </p>
                </div>
            </div>

            <AchievementForm
                mode="edit"
                initial={achievement}
                action={action}
                submitLabel="Save changes"
            />
        </div>
    );
}
