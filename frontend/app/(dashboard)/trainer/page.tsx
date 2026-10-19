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
      </header>

      <section className="glass-panel p-6 sm:p-8">
        <header className="mb-4 flex items-center gap-3">
          <span className="icon-chip h-10 w-10 text-sandy-brown-700 dark:text-sandy-brown-300"></span>
          <div>
            <h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
              Pending requests
            </h2>
          </div>
        </header>
        <TrainerPendingRequests />
      </section>

      <section className="glass-panel p-6 sm:p-8">
        <header className="mb-4 flex items-center gap-3">
          <span className="icon-chip h-10 w-10 text-verdigris-700 dark:text-verdigris-300"></span>
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
