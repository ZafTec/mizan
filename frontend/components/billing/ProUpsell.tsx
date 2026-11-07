import type { IconName } from "@/components/ui/icon";
import Link from "next/link";

interface ProUpsellProps {
  title: string;
  message: string;
  icon?: IconName;
  checkoutPlan?: "pro" | "pro-yearly";
}

export function ProUpsell({
  title,
  message,
  icon = "lock",
  checkoutPlan = "pro",
}: ProUpsellProps) {
  return (
    <div className="card overflow-visible flex flex-col items-center gap-4 p-10 text-center">
      <span className="inline-flex items-center justify-center text-charcoal-blue-700 dark:text-charcoal-blue-300 h-14 w-14"></span>
      <div className="space-y-1.5">
        <h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
          {title}
        </h3>
        <p className="max-w-md text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
          {message}
        </p>
      </div>
      <Link href={`/billing?checkout=${checkoutPlan}`} className="btn-primary">
        Upgrade to Pro
      </Link>
    </div>
  );
}
