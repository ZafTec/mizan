import Link from "next/link";
import { Icon } from "@/components/ui/icon";

/**
 * The proof card is the product, not an illustration of it: same flat
 * card, same tabular numbers, same macro tones used on /dashboard. Copy
 * matches design/Landing.dc.html exactly.
 */
export function HeroSection() {
	return (
		<section
			data-testid="hero-section"
			aria-labelledby="hero-heading"
			className="grid grid-cols-1 items-center gap-10 py-10 sm:py-14 lg:grid-cols-[1.05fr_0.95fr] lg:gap-16"
		>
			<div className="flex max-w-2xl flex-col gap-6">
				<p className="eyebrow">Meals · Workouts · Measurements</p>
				<h1
					id="hero-heading"
					className="text-[2.75rem] leading-[1.03] font-medium tracking-tight text-charcoal-blue-900 sm:text-6xl dark:text-charcoal-blue-50"
				>
					The tracker you
					<br />
					actually keep using.
				</h1>
				<p className="max-w-md text-[17px] leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
					Every food tracker dies the week logging becomes a chore. Mizan is built around one number: a meal, a set or a weigh-in goes in under ten seconds — from the web, from Telegram, or by telling your AI assistant.
				</p>
				<div className="flex items-center gap-3">
					<Link href="/register" className="btn-primary">
						Start logging free
						<Icon name="arrowRight" size={15} aria-hidden="true" />
					</Link>
					<Link href="#thesis" className="btn-secondary">
						Read the thesis
					</Link>
				</div>
				<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-500">
					Free tier, no card. Self-host it instead if you would rather own the database.
				</p>
			</div>

			<div className="card">
				<div className="flex items-center justify-between border-b border-charcoal-blue-200 px-5 py-4 dark:border-charcoal-blue-700">
					<span className="text-lg font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50" style={{ fontFamily: "var(--font-serif)" }}>
						Tuesday, 26 August
					</span>
					<span className="flex items-center gap-1.5 text-xs font-semibold text-burnt-peach-700 dark:text-burnt-peach-400">
						<Icon name="flame" size={13} aria-hidden="true" />
						12 days · resets 09:41
					</span>
				</div>

				<div className="flex flex-col gap-3 border-b border-charcoal-blue-100 px-5 py-4 dark:border-charcoal-blue-800">
					<div className="flex items-baseline justify-between">
						<span className="eyebrow">Calories</span>
						<span className="num text-[13px] text-charcoal-blue-600 dark:text-charcoal-blue-400">
							<b className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">1,840</b> / 2,200 kcal
						</span>
					</div>
					<div className="h-1 overflow-hidden rounded-full bg-charcoal-blue-100 dark:bg-charcoal-blue-800">
						<div className="h-full rounded-full bg-verdigris-700 dark:bg-verdigris-400" style={{ width: "84%" }} />
					</div>
					<div className="grid grid-cols-3 gap-3 pt-0.5">
						<MacroReadout label="Protein" value="142" target="/ 160 g" />
						<MacroReadout label="Carbs" value="188" target="/ 220 g" />
						<MacroReadout label="Fat" value="61" target="/ 73 g" />
					</div>
				</div>

				<div className="flex flex-col">
					<MealRow name="Shiro wat & injera" meta="Lunch · 1 serving" kcal="612 kcal" />
					<MealRow name="Greek yoghurt, honey" meta="Breakfast · 200 g" kcal="248 kcal" />
					<div className="flex items-center gap-3 px-5 py-3 bg-charcoal-blue-50 dark:bg-charcoal-blue-900">
						<Icon name="messageCircle" size={15} className="shrink-0 text-verdigris-700 dark:text-verdigris-400" aria-hidden="true" />
						<div className="min-w-0 flex-1 text-[13px] text-verdigris-700 dark:text-verdigris-400">
							From Telegram — photo of dinner, confirmed
						</div>
						<div className="num text-xs text-verdigris-700 dark:text-verdigris-400">7s ago</div>
					</div>
				</div>
			</div>
		</section>
	);
}

function MacroReadout({ label, value, target }: { label: string; value: string; target: string }) {
	return (
		<div>
			<div className="eyebrow">{label}</div>
			<div className="num mt-0.5 text-[13px]">
				<b className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{value}</b>{" "}
				<span className="text-charcoal-blue-500 dark:text-charcoal-blue-500">{target}</span>
			</div>
		</div>
	);
}

function MealRow({ name, meta, kcal }: { name: string; meta: string; kcal: string }) {
	return (
		<div className="flex items-center gap-3 border-b border-charcoal-blue-100 px-5 py-3 last:border-b-0 dark:border-charcoal-blue-800">
			<div className="min-w-0 flex-1">
				<div className="text-[13.5px] font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">{name}</div>
				<div className="text-[11.5px] text-charcoal-blue-500 dark:text-charcoal-blue-500">{meta}</div>
			</div>
			<div className="num text-[13px] text-charcoal-blue-600 dark:text-charcoal-blue-400">{kcal}</div>
		</div>
	);
}
