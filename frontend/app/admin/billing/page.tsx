import { getAdminBillingCatalog } from "@/data/subscription";
import BillingAdmin from "./BillingAdmin";

export const dynamic = "force-dynamic";

export const metadata = {
	title: "Billing | Mizan admin",
	description: "The Pro plans and deals on sale, created in Paddle",
};

export default async function AdminBillingPage() {
	const catalog = await getAdminBillingCatalog();

	return (
		<div className="space-y-6">
			<header className="space-y-1">
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">Billing</h1>
				<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					What Pro costs. Every change is made in Paddle first, then shows on the pricing page and at checkout.
				</p>
			</header>
			{catalog ? (
				<BillingAdmin catalog={catalog} />
			) : (
				<p className="card p-6 text-sm text-red-700 dark:text-red-400">The billing catalogue could not be loaded.</p>
			)}
		</div>
	);
}
