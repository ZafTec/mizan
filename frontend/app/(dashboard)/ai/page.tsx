import Link from "next/link";
import AiChat, { type QuickPrompt } from "@/components/ai/AiChat";

export const dynamic = "force-dynamic";

const QUICK_PROMPTS: QuickPrompt[] = [
	{
		id: "fit-remaining",
		label: "Ideas for my remaining macros today",
		prompt: "What are 3 quick meal ideas that fit my remaining macros for today?",
		icon: "flame",
	},
	{
		id: "weekly-review",
		label: "Review my last 7 days",
		prompt: "Summarise my nutrition and training over the past 7 days and flag any trends.",
		icon: "chart",
	},
	{
		id: "protein-gap",
		label: "High-protein snack ideas",
		prompt: "Give me 5 high-protein snacks under 250 calories I can keep on hand.",
		icon: "sparkles",
	},
	{
		id: "workout-plan",
		label: "What to focus on in my next workout",
		prompt: "Based on my recent workouts, what should I focus on in my next session?",
		icon: "activity",
	},
];

export default function AiHubPage() {
	return (
		<div className="space-y-4">
			<header className="flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-1">
					<p className="eyebrow">Assistant</p>
					<h1 className="text-2xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-3xl">
						Ask about your day
					</h1>
				</div>
				<Link href="/profile/settings" className="text-sm underline underline-offset-4">
					Choose what the assistant can see
				</Link>
			</header>

			<AiChat quickPrompts={QUICK_PROMPTS} />
		</div>
	);
}
