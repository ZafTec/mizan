import { Icon, type IconName } from "@/components/ui/icon";

const DOORS: { icon: IconName; title: string; body: string }[] = [
	{
		icon: "activity",
		title: "The web app",
		body: "Search, log, done — three keystrokes from anywhere in the app. Meal plans, recipes with preparations, a shopping list that adds itself up, and a workout log with real templates.",
	},
	{
		icon: "messageCircle",
		title: "Telegram",
		body: "Send a photo of what you are eating. It comes back as an estimate with a Confirm button — never a silent write. Or just talk to it; it is the same assistant as the website.",
	},
	{
		icon: "brain",
		title: "Your AI assistant",
		body: "An MCP server with the whole product behind it, so Claude or any MCP client can log your day, pull your week, or plan your meals — authorised as you, never as a service account.",
	},
];

export function ThreeDoorsSection() {
	return (
		<section id="thesis" aria-labelledby="doors-heading" className="border-t border-charcoal-blue-200 py-16 sm:py-20">
			<div className="mb-10 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between sm:gap-10">
				<h2 id="doors-heading" className="max-w-[16ch] text-3xl font-medium leading-tight tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					Three ways in. One log.
				</h2>
				<p className="max-w-md text-sm leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
					The reason people stop logging is friction at the moment they eat. So Mizan meets you wherever that moment happens, and it is the same database underneath — a conversation you start on your phone continues in the browser.
				</p>
			</div>

			<div className="grid grid-cols-1 gap-px border border-charcoal-blue-200 bg-charcoal-blue-200 sm:grid-cols-3 dark:border-charcoal-blue-700 dark:bg-charcoal-blue-700">
				{DOORS.map((door) => (
					<div key={door.title} className="flex flex-col gap-3 bg-white p-7 dark:bg-charcoal-blue-950">
						<Icon name={door.icon} size={22} strokeWidth={1.6} className="text-charcoal-blue-900 dark:text-charcoal-blue-50" aria-hidden="true" />
						<h3 className="text-xl font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
							{door.title}
						</h3>
						<p className="text-[13.5px] leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">{door.body}</p>
					</div>
				))}
			</div>
		</section>
	);
}
