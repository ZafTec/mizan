import TemplateManager from "@/components/planning/TemplateManager";
import { listTemplatesServer } from "@/lib/api/mealPlans.server";

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
    <div className="log-page space-y-10" data-testid="admin-meal-plans-page">
      <header className="log-page-header block!">
        <p className="eyebrow">Administration</p>
        <h1 className="mt-1">Meal plans</h1>
        <p className="log-muted mt-2">
          Templates everyone can start a plan from.
        </p>
      </header>

      <section aria-label="Template summary" className="facts-strip">
        <div>
          <p className="text-sm log-muted">Global templates</p>
          <p className="num nutrition-value">{templates.length}</p>
        </div>
        <div>
          <p className="text-sm log-muted">Entries</p>
          <p className="num nutrition-value">{entries}</p>
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
