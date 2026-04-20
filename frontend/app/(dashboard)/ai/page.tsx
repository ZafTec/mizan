import { AnimatedIcon } from "@/components/ui/animated-icon";
import AiChat from "@/components/ai/AiChat";

export const dynamic = "force-dynamic";

const QUICK_PROMPTS: Array<{ id: string; label: string; prompt: string; icon: Parameters<typeof AnimatedIcon>[0]["name"] }> = [
	{
		id: "fit-remaining",
		label: "Ideas for my remaining macros today",
		prompt: "What are 3 quick meal ideas that fit my remaining macros for today?",
		icon: "flame",
	},
	{
		id: "weekly-review",
		label: "Review last 7 days",
		prompt: "Summarise my nutrition and training over the past 7 days and flag any trends.",
		icon: "chartLine",
	},
	{
		id: "protein-gap",
		label: "Protein ideas",
		prompt: "Give me 5 high-protein snacks under 250 calories I can keep on hand.",
		icon: "sparkles",
	},
	{
		id: "workout-plan",
		label: "Next workout suggestion",
		prompt: "Based on my recent workouts, what should I focus on in my next session?",
		icon: "activity",
	},
];

export default async function AiHubPage() {
	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">AI Coach</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Nutrition assistant
					</h1>
					<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Chat with your AI coach, ask for meal suggestions, or get quick analysis of your day. Your macros, goals, and recent logs are used for context.
					</p>
				</div>
			</header>

			<div className="grid gap-6 lg:grid-cols-[1.65fr_1fr]">
				<AiChat quickPrompts={QUICK_PROMPTS} />

				<aside className="space-y-4">
					<section className="glass-panel p-6">
						<div className="mb-4 flex items-center gap-3">
							<span className="icon-chip h-10 w-10 text-verdigris-700 dark:text-verdigris-300">
								<AnimatedIcon name="brain" size={18} />
							</span>
							<div>
								<h2 className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
									Weekly pulse
								</h2>
								<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
									Generated every Sunday
								</p>
							</div>
						</div>
						<div className="space-y-3 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
							<p>
								Ask the assistant for a weekly summary using the quick prompt above. A dedicated scheduled digest will drop into this card once enabled.
							</p>
						</div>
					</section>

					<section className="glass-panel p-6">
						<div className="mb-4 flex items-center gap-3">
							<span className="icon-chip h-10 w-10 text-sandy-brown-700 dark:text-sandy-brown-300">
								<AnimatedIcon name="sparkles" size={18} />
							</span>
							<div>
								<h2 className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
									Vision analysis
								</h2>
								<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
									Coming soon
								</p>
							</div>
						</div>
						<p className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
							Upload a photo of your meal and the assistant will estimate portions and log it for you. Enable the vision model from Admin → Settings when ready.
						</p>
					</section>

					<section className="glass-panel p-6">
						<div className="mb-4 flex items-center gap-3">
							<span className="icon-chip h-10 w-10 text-burnt-peach-700 dark:text-burnt-peach-300">
								<AnimatedIcon name="badgeAlert" size={18} />
							</span>
							<div>
								<h2 className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
									Good to know
								</h2>
							</div>
						</div>
						<ul className="space-y-2 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
							<li className="flex gap-2">
								<span className="text-verdigris-600">•</span> Responses use your current goals and recent logs. No training data leaves your account.
							</li>
							<li className="flex gap-2">
								<span className="text-verdigris-600">•</span> The assistant can't update goals or log meals for you. It returns suggestions you can accept manually.
							</li>
						</ul>
					</section>
				</aside>
			</div>
		</div>
	);
}
