import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { getUserServer } from "@/helper/session";
import { serverApi } from "@/lib/api.server";
import MessagingThread from "@/components/Messaging/MessagingThread";
import type { MyTrainerDto, TrainerClientDto } from "@/types/api-contracts";

export const dynamic = "force-dynamic";

interface ConversationSummary {
	relationshipId: string;
	otherPartyId: string;
	otherPartyName: string;
	otherPartyImage: string | null;
	otherPartyRoleLabel: string;
	status: string;
	canMessage: boolean;
}

async function loadMyTrainer(): Promise<ConversationSummary | null> {
	try {
		const data = await serverApi<MyTrainerDto | null>("/api/Trainers/my-trainer");
		if (!data) return null;
		return {
			relationshipId: data.relationshipId,
			otherPartyId: data.trainerId,
			otherPartyName: data.trainerName || data.trainerEmail || "Your coach",
			otherPartyImage: data.trainerImage ?? null,
			otherPartyRoleLabel: "Coach",
			status: data.status,
			canMessage: data.canMessage,
		};
	} catch {
		return null;
	}
}

async function loadTrainerClients(): Promise<ConversationSummary[]> {
	try {
		const data = await serverApi<{ items: TrainerClientDto[] }>("/api/Trainers/clients");
		return (data?.items ?? []).map((c) => ({
			relationshipId: c.relationshipId,
			otherPartyId: c.clientId,
			otherPartyName: c.clientName || c.clientEmail || "Client",
			otherPartyImage: null,
			otherPartyRoleLabel: "Client",
			status: c.status,
			canMessage: c.canMessage,
		}));
	} catch {
		return [];
	}
}

export default async function MessagingPage({
	searchParams,
}: {
	searchParams: Promise<{ r?: string }>;
}) {
	const user = await getUserServer();
	const { r: selectedRelationship } = await searchParams;

	const isTrainer = user.role === "trainer" || user.role === "admin";

	const [clientList, myTrainer] = await Promise.all([
		isTrainer ? loadTrainerClients() : Promise.resolve([]),
		isTrainer ? Promise.resolve<ConversationSummary | null>(null) : loadMyTrainer(),
	]);

	const conversations: ConversationSummary[] = isTrainer
		? clientList
		: myTrainer
			? [myTrainer]
			: [];

	const active =
		conversations.find((c) => c.relationshipId === selectedRelationship) ??
		conversations[0] ??
		null;

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Direct</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Messaging
					</h1>
					<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						{isTrainer
							? "Coaching conversations with each of your active clients."
							: "Send questions, updates and progress notes to your coach."}
					</p>
				</div>
			</header>

			{conversations.length === 0 ? (
				<section className="glass-panel flex flex-col items-center justify-center gap-3 p-12 text-center">
					<span className="icon-chip h-14 w-14 text-verdigris-700 dark:text-verdigris-300">
						<AnimatedIcon name="messageCircle" size={22} />
					</span>
					<p className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						No conversations yet
					</p>
					<p className="max-w-md text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						{isTrainer
							? "You'll see conversations here as soon as clients start messaging you."
							: "Connect with a coach to start messaging."}
					</p>
					{!isTrainer && (
						<Link href="/trainers" className="btn-primary !rounded-2xl !py-2 text-sm">
							Find a coach
							<AnimatedIcon name="arrowRight" size={14} />
						</Link>
					)}
				</section>
			) : (
				<div className="grid min-h-[calc(100vh-260px)] gap-4 lg:grid-cols-[320px_1fr]">
					<aside className="glass-panel flex flex-col overflow-hidden p-0">
						<header className="border-b border-charcoal-blue-200/70 p-4 dark:border-white/10">
							<div className="flex items-center gap-2">
								<span className="icon-chip h-9 w-9 text-verdigris-700 dark:text-verdigris-300">
									<AnimatedIcon name="users" size={15} />
								</span>
								<div>
									<p className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
										Conversations
									</p>
									<p className="text-[11px] text-charcoal-blue-500 dark:text-charcoal-blue-400">
										{conversations.length} active
									</p>
								</div>
							</div>
						</header>
						<div className="flex-1 space-y-1 overflow-y-auto p-2">
							{conversations.map((c) => {
								const isActive = active?.relationshipId === c.relationshipId;
								return (
									<Link
										key={c.relationshipId}
										href={`/messaging?r=${c.relationshipId}`}
										className={`flex items-center gap-3 rounded-2xl px-3 py-3 text-sm transition-colors ${
											isActive
												? "bg-verdigris-500/15 text-charcoal-blue-900 dark:bg-verdigris-500/20 dark:text-charcoal-blue-50"
												: "text-charcoal-blue-700 hover:bg-white/70 dark:text-charcoal-blue-200 dark:hover:bg-white/5"
										}`}
									>
										<span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-verdigris-600 text-sm font-semibold text-white">
											{c.otherPartyName.charAt(0).toUpperCase()}
										</span>
										<div className="min-w-0 flex-1">
											<p className="truncate font-medium">{c.otherPartyName}</p>
											<p className="truncate text-[11px] text-charcoal-blue-500 dark:text-charcoal-blue-400">
												{c.otherPartyRoleLabel} · {c.status}
											</p>
										</div>
									</Link>
								);
							})}
						</div>
					</aside>

					{active ? (
						<MessagingThread
							relationshipId={active.relationshipId}
							currentUserId={user.id}
							otherPartyName={active.otherPartyName}
							otherPartyRoleLabel={active.otherPartyRoleLabel}
							canMessage={active.canMessage}
						/>
					) : (
						<section className="glass-panel flex items-center justify-center p-12 text-center">
							<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
								Pick a conversation to start messaging.
							</p>
						</section>
					)}
				</div>
			)}
		</div>
	);
}
