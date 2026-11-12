import AchievementForm, { EMPTY_DRAFT } from "../AchievementForm";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export const metadata = { title: "New achievement | Mizan admin" };

export default function NewAchievementPage() {
  return (
    <div className="max-w-2xl">
      <AdminPageHeader
        title="New achievement"
        lead="What it is called, what earns it and how many points it is worth."
        back={{ href: "/admin/achievements", label: "Achievements" }}
      />

      <AchievementForm initial={EMPTY_DRAFT} />
    </div>
  );
}
