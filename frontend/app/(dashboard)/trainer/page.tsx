import Link from "next/link";
import { ClientList } from "@/components/trainer/ClientList";
import { TrainerPendingRequests } from "@/components/trainer/TrainerPendingRequests";

export const dynamic = "force-dynamic";

export default function TrainerDashboard() {
  return (
    <div className="space-y-6 lg:space-y-8">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="space-y-2">
          <p className="eyebrow">Coaching</p>
          <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
            Trainer dashboard
          </h1>
        </div>
        <Link
          href="/trainer/templates"
          className="btn-secondary min-h-11 self-start sm:self-auto"
        >
          Meal plan templates
        </Link>
      </header>

      <section className="card overflow-visible p-6 sm:p-8">
        <header className="mb-4 flex items-center gap-3">
          <span className="inline-flex items-center justify-center text-charcoal-blue-700 dark:text-charcoal-blue-300 h-10 w-10"></span>
          <div>
            <h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
              Pending requests
            </h2>
          </div>
        </header>
        <TrainerPendingRequests />
      </section>

      <section className="card overflow-visible p-6 sm:p-8">
        <header className="mb-4 flex items-center gap-3">
          <span className="inline-flex items-center justify-center text-charcoal-blue-700 dark:text-charcoal-blue-300 h-10 w-10"></span>
          <div>
            <h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
              Your clients
            </h2>
          </div>
        </header>
        <ClientList />
      </section>
    </div>
  );
}
