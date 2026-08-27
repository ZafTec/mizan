import Link from "next/link";
import { Icon, type IconName } from "@/components/ui/icon";

interface ProUpsellProps {
  title: string;
  message: string;
  icon?: IconName;
  checkoutPlan?: "pro" | "pro-yearly" | "lifetime";
}

export function ProUpsell({ title, message, icon = "lock", checkoutPlan = "pro" }: ProUpsellProps) {
  return (
    <div className="glass-panel flex flex-col items-center gap-4 p-10 text-center">
      <span className="icon-chip h-14 w-14 text-brand-700 dark:text-brand-300">
        <Icon name={icon} size={22} aria-hidden="true" />
      </span>
      <div className="space-y-1.5">
        <h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{title}</h3>
        <p className="max-w-md text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{message}</p>
      </div>
      <Link href={`/billing?checkout=${checkoutPlan}`} className="btn-primary">
        <Icon name="sparkles" size={16} aria-hidden="true" />
        Upgrade to Pro
      </Link>
    </div>
  );
}
