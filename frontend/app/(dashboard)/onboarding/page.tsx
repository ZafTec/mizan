import Link from "next/link";
import { redirect } from "next/navigation";
import { getCurrentUser } from "@/lib/auth";
import OnboardingAgent from "@/components/ai/OnboardingAgent";

export const metadata = {
	title: "Get set up | Mizan",
	description: "Set your targets and first measurement through a short conversation",
};

export default async function OnboardingPage() {
	const user = await getCurrentUser();
	if (!user) redirect("/login");

	return (
		<div className="mx-auto max-w-2xl space-y-6">
			<header className="space-y-2">
				<p className="eyebrow">Setup</p>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
					Get set up
				</h1>
				<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Tell it what you are after and it will record your targets and first
					measurement as you go. Everything it does is shown, and you can change
					any of it later.
				</p>
			</header>

			<OnboardingAgent />

			<p className="text-center text-xs text-charcoal-blue-400 dark:text-charcoal-blue-500">
				Would rather fill in forms?{" "}
				<Link href="/goal" className="hover:underline">
					Set your goal directly
				</Link>
			</p>
		</div>
	);
}
