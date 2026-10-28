import Link from "next/link";
import { getUserServer } from "@/helper/session";
import { dateInTimeZone } from "@/lib/log-date";
import {
  getTrainerClientServer,
  listTemplatesServer,
} from "@/lib/api/mealPlans.server";
import NewPlanForm from "@/components/planning/NewPlanForm";
import { logger } from "@/lib/logger";

const log = logger.createModuleLogger("trainer-client-plan");

export const dynamic = "force-dynamic";
export const metadata = { title: "Draft a plan · Mizan" };

/**
 * A coach drafts a plan for a client. The client controls `canPlanMeals`;
 * without it the page says so and offers nothing else.
 */
export default async function ClientPlanPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const [{ id }, user] = await Promise.all([params, getUserServer()]);
  const [client, templates] = await Promise.all([
    getTrainerClientServer(id).catch((error) => {
      log.error("Failed to load the client", {
        error: error instanceof Error ? error.message : String(error),
      });
      return null;
    }),
    listTemplatesServer().catch((error) => {
      log.error("Failed to load templates", {
        error: error instanceof Error ? error.message : String(error),
      });
      return [];
    }),
  ]);
  const name = client?.clientName || client?.clientEmail || "This client";
  return (
    <div className="log-page space-y-8" data-testid="trainer-client-plan-page">
      <header className="log-page-header block!">
        <p className="text-sm">
          <Link href={`/trainer/clients/${id}`} className="text-link">
            {name}
          </Link>
        </p>
        <h1 className="mt-1">Draft a plan</h1>
        <p className="text-muted-foreground mt-2">
          The draft reaches {name} to accept or decline. Start from one of{" "}
          <Link href="/trainer/templates" className="text-link">
            your templates
          </Link>{" "}
          or an empty plan.
        </p>
      </header>
      {client?.canPlanMeals ? (
        <NewPlanForm
          templates={templates}
          household={null}
          today={dateInTimeZone(user.timeZoneId || "UTC")}
          clientId={client.clientId}
          clientName={name}
        />
      ) : (
        <p className="log-empty">{name} has not granted meal planning.</p>
      )}
    </div>
  );
}
