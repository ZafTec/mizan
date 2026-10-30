import Link from "next/link";
import { getUserServer } from "@/helper/session";
import TemplateManager from "@/components/planning/TemplateManager";
import { listTemplatesServer } from "@/lib/api/mealPlans.server";

export const dynamic = "force-dynamic";
export const metadata = { title: "Templates · Mizan" };

/**
 * /trainer/templates: a coach's own meal plan templates. Only the coach sees
 * them; they draft a client's plan from one on /trainer/clients/[id]/plan,
 * beside the global templates.
 */
export default async function TrainerTemplatesPage() {
  const user = await getUserServer();
  const templates = (await listTemplatesServer()).filter(
    (template) => !template.global && template.ownerId === user.id,
  );
  return (
    <div className="log-page space-y-10" data-testid="trainer-templates-page">
      <header className="log-page-header block!">
        <p className="text-sm">
          <Link href="/trainer" className="text-link">
            Coaching
          </Link>
        </p>
        <h1 className="mt-1">Templates</h1>
        <p className="text-muted-foreground mt-2">
          Weeks you plan once and draft for any client.
        </p>
      </header>

      <section aria-label="Template summary" className="facts-strip">
        <div>
          <p className="text-sm text-muted-foreground">Your templates</p>
          <p className="tabular-nums nutrition-value">{templates.length}</p>
        </div>
      </section>

      <TemplateManager
        templates={templates}
        global={false}
        basePath="/trainer/templates"
        empty="You have no templates yet."
      />
    </div>
  );
}
