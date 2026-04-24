"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { respondToInvitation, type HouseholdInvitationSummary } from "@/data/household";
import { appToast } from "@/lib/toast";

export function InvitationInbox({ invitations }: { invitations: HouseholdInvitationSummary[] }) {
	const router = useRouter();
	const [pending, startTransition] = useTransition();

	const respond = (id: string, action: "accept" | "decline") => {
		startTransition(async () => {
			const result = await respondToInvitation(id, action);
			if (result.success) {
				appToast.success(action === "accept" ? "Joined household." : "Invitation declined.");
				router.refresh();
			} else {
				appToast.error(result.message ?? "Could not update invitation.");
			}
		});
	};

	return (
		<section className="card p-5 sm:p-6" aria-label="Pending invitations">
			<div className="mb-4 flex items-center gap-2 text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
				<AnimatedIcon name="bell" size={16} aria-hidden="true" />
				Invitations waiting for you
			</div>
			<ul className="space-y-3">
				{invitations.map((inv) => (
					<li
						key={inv.id}
						className="flex flex-col gap-3 rounded-2xl border border-charcoal-blue-100 bg-charcoal-blue-50/40 p-4 sm:flex-row sm:items-center sm:justify-between dark:border-white/10 dark:bg-charcoal-blue-900/40"
					>
						<div>
							<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{inv.householdName}
							</p>
							<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
								Invited by {inv.invitedByName} as <span className="font-medium">{inv.role}</span> ·
								expires {new Date(inv.expiresAt).toLocaleDateString()}
							</p>
						</div>
						<div className="flex items-center gap-2">
							<button
								type="button"
								onClick={() => respond(inv.id, "decline")}
								className="btn-ghost h-9"
								disabled={pending}
							>
								Decline
							</button>
							<button
								type="button"
								onClick={() => respond(inv.id, "accept")}
								className="btn-primary h-9"
								disabled={pending}
							>
								Accept
							</button>
						</div>
					</li>
				))}
			</ul>
		</section>
	);
}
