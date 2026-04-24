import Link from "next/link";
import { notFound } from "next/navigation";
import { getHousehold, getHouseholdPendingInvites } from "@/data/household";
import AdminMemberRow from "./AdminMemberRow";

export const dynamic = "force-dynamic";

export const metadata = {
	title: "Household detail | Mizan admin",
};

export default async function AdminHouseholdDetail({
	params,
}: {
	params: Promise<{ id: string }>;
}) {
	const { id } = await params;
	const [detail, pendingInvites] = await Promise.all([
		getHousehold(id),
		getHouseholdPendingInvites(id),
	]);

	if (!detail) {
		notFound();
	}

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="space-y-2">
				<Link href="/admin/households" className="text-xs uppercase tracking-[0.14em] text-brand-700 hover:underline dark:text-brand-300">
					← All households
				</Link>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					{detail.name}
				</h1>
				<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Created {new Date(detail.createdAt).toLocaleString()} · {detail.members.length} members
				</p>
			</header>

			<section className="card p-5 sm:p-6">
				<h2 className="mb-4 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Members</h2>
				<ul className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
					{detail.members.map((m) => (
						<AdminMemberRow key={m.userId} householdId={detail.id} member={m} />
					))}
				</ul>
			</section>

			{pendingInvites.length > 0 && (
				<section className="card p-5 sm:p-6">
					<h2 className="mb-4 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						Pending invitations
					</h2>
					<ul className="space-y-2">
						{pendingInvites.map((inv) => (
							<li
								key={inv.id}
								className="flex items-center justify-between rounded-2xl border border-dashed border-charcoal-blue-200 bg-charcoal-blue-50/40 px-4 py-3 text-sm dark:border-white/10 dark:bg-charcoal-blue-900/40"
							>
								<div>
									<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
										{inv.invitedName ?? inv.invitedEmail}
									</p>
									<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
										{inv.invitedEmail} · {inv.role} · expires {new Date(inv.expiresAt).toLocaleDateString()}
									</p>
								</div>
							</li>
						))}
					</ul>
				</section>
			)}
		</div>
	);
}
