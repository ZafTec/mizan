import TemplateManager from "@/components/planning/TemplateManager";
import { listTemplatesServer } from "@/lib/api/mealPlans.server";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Meal plans | Mizan admin",
  description: "Global meal plan templates everyone can start from",
};

/**
 * /admin/meal-plans: the global templates (docs/PLANNING.md#templates).
 * Anyone applies one from /meal-plan/new; a coach also drafts a client's plan
 * from one. Coaches' own templates are theirs and are not listed here.
 */
export default async function AdminMealPlansPage() {
  const templates = (await listTemplatesServer()).filter(
    (template) => template.global,
  );
  const entries = templates.reduce(
    (count, template) => count + template.entryCount,
    0,
  );
  return (
    <div
      className="flex flex-col gap-10 [&>header]:mb-0 [&_h2]:text-[20px] [&_h2]:leading-[1.3] [&_h2]:font-semibold"
      data-testid="admin-meal-plans-page"
    >
      <AdminPageHeader
        title="Meal plans"
        lead="Templates everyone can start a plan from."
      />

      <section aria-label="Template summary" className="facts-strip">
        <div>
          <p className="text-sm text-muted-foreground">Global templates</p>
          <p className="tabular-nums nutrition-value">{templates.length}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Entries</p>
          <p className="tabular-nums nutrition-value">{entries}</p>
        </div>
      </section>

      <TemplateManager
        templates={templates}
        global
        basePath="/admin/meal-plans"
        empty="No global templates yet."
      />
    </div>
  );
}
