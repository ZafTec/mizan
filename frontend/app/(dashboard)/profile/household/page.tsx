import Link from "next/link";
import { getMyHouseholds, getHousehold, getHouseholdPendingInvites } from "@/data/household";
import { HouseholdSwitcherForm, CreateHouseholdForm } from "./parts/ActiveHouseholdControls";
import { InvitationInbox } from "./parts/InvitationInbox";
import { MemberList } from "./parts/MemberList";
import { InviteForm } from "./parts/InviteForm";
import { LeaveHouseholdButton } from "./parts/LeaveHouseholdButton";

export const dynamic = "force-dynamic";

export const metadata = {
	title: "Household | Mizan",
	description: "Manage the households you share recipes, meal plans, and shopping lists with.",
};

export default async function HouseholdSettingsPage({
	searchParams,
}: {
	searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
	const params = await searchParams;
	const my = await getMyHouseholds();

	const focusedId =
		(typeof params.household === "string" && params.household) ||
		my.activeHouseholdId ||
		my.households[0]?.id ||
		null;

	const [detail, pendingInvites] = focusedId
		? await Promise.all([getHousehold(focusedId), getHouseholdPendingInvites(focusedId)])
		: [null, [] as Awaited<ReturnType<typeof getHouseholdPendingInvites>>];

	const focusedSummary = focusedId ? my.households.find((h) => h.id === focusedId) : null;
	const isAdmin = focusedSummary?.myRole === "admin" || focusedSummary?.myRole === "owner";

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Shared workspace</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Your households
					</h1>
					<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Share recipes, meal plans, and shopping lists with the people you cook and train with.
					</p>
				</div>
			</header>

			{my.pendingInvitations.length > 0 && (
				<InvitationInbox invitations={my.pendingInvitations} />
			)}

			<section className="card p-5 sm:p-6">
				<div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
					<div>
						<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							Active household
						</h2>
						<p className="mt-1 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							The household that new recipes, meal plans, and shopping lists are shared to by default.
						</p>
					</div>
					<HouseholdSwitcherForm
						households={my.households}
						activeId={my.activeHouseholdId ?? null}
					/>
				</div>
			</section>

			<div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
				<div className="lg:col-span-2 space-y-6">
					{detail ? (
						<section className="card p-5 sm:p-6">
							<div className="mb-5 flex items-start justify-between gap-4">
								<div>
									<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
										{detail.name}
									</h2>
									<p className="mt-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
										{detail.members.length} member{detail.members.length === 1 ? "" : "s"}
										{focusedSummary?.myRole ? ` • you are ${focusedSummary.myRole}` : ""}
									</p>
								</div>
								<LeaveHouseholdButton
									householdId={detail.id}
									disabled={!focusedSummary}
								/>
							</div>
							<MemberList
								householdId={detail.id}
								members={detail.members}
								pendingInvites={pendingInvites}
								canManage={isAdmin}
							/>
						</section>
					) : (
						<section className="card p-8 text-center text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							You&apos;re not a member of any household yet. Create one below or accept an invitation.
						</section>
					)}

					{isAdmin && detail && (
						<section className="card p-5 sm:p-6">
							<h2 className="mb-3 text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								Invite someone
							</h2>
							<InviteForm householdId={detail.id} />
						</section>
					)}
				</div>

				<div className="space-y-6">
					<section className="card p-5 sm:p-6">
						<h2 className="mb-3 text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							All your households
						</h2>
						<ul className="space-y-2">
							{my.households.length === 0 && (
								<li className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
									None yet.
								</li>
							)}
							{my.households.map((h) => (
								<li key={h.id}>
									<Link
										href={`/profile/household?household=${h.id}`}
										className={`block rounded-2xl border px-3 py-2 text-sm transition-colors duration-150 ease-out ${
											h.id === focusedId
												? "border-brand-500/40 bg-brand-500/5 text-charcoal-blue-900 dark:bg-brand-500/10 dark:text-charcoal-blue-50"
												: "border-charcoal-blue-100 hover:border-brand-500/30 dark:border-white/10"
										}`}
									>
										<div className="flex items-center justify-between">
											<span className="font-medium">{h.name}</span>
											{h.isActive && (
												<span className="text-[10px] font-semibold uppercase tracking-[0.14em] text-brand-700 dark:text-brand-300">
													active
												</span>
											)}
										</div>
										<div className="mt-0.5 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
											{h.memberCount} member{h.memberCount === 1 ? "" : "s"} • you are {h.myRole}
										</div>
									</Link>
								</li>
							))}
						</ul>
					</section>

					<section className="card p-5 sm:p-6">
						<h2 className="mb-3 text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							Create a new household
						</h2>
						<CreateHouseholdForm />
					</section>
				</div>
			</div>
		</div>
	);
}
