import { notFound } from "next/navigation";
import { getAdminAchievement } from "@/data/admin/achievements";
import AchievementForm from "../../AchievementForm";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export default async function EditAchievementPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const achievement = await getAdminAchievement(id);
  if (!achievement) notFound();

  return (
    <div className="max-w-2xl">
      <AdminPageHeader
        title={achievement.name}
        lead="Changes apply to everyone, including people who already unlocked it."
        back={{ href: "/admin/achievements", label: "Achievements" }}
      />

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
