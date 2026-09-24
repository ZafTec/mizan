import { getBillingPlans } from "@/data/subscription";
import BillingView from "./BillingView";

export const metadata = { title: "Billing | Mizan" };

export default async function BillingPage() {
	const plans = await getBillingPlans();
	return <BillingView plans={plans} />;
}
