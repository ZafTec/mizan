import Link from "next/link";
import { listAiPrompts, getGlobalAiUsage } from "@/data/admin/ai";

export const metadata = {
	title: "Assistant | Mizan admin",
	description: "Prompt versions, evals and spend",
};

function currency(micros: number) {
	return `$${(micros / 1_000_000).toFixed(2)}`;
}

function Meter({ used, ceiling }: { used: number; ceiling: number }) {
	const pct = ceiling > 0 ? Math.min(100, (used / ceiling) * 100) : 0;
	return (
		<div className="h-1.5 w-full overflow-hidden rounded-full bg-charcoal-blue-200 dark:bg-white/10">
			<div
				className={pct > 85 ? "h-full bg-red-500" : "h-full bg-verdigris-500"}
				style={{ width: `${pct}%` }}
			/>
		</div>
	);
}

export default async function AdminAiPage() {
	const [prompts, usage] = await Promise.all([listAiPrompts(), getGlobalAiUsage()]);

	return (
		<div className="space-y-8">
			<header className="space-y-2">
				<p className="eyebrow">Administration</p>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					Assistant
				</h1>
				<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					What the assistant says is a product surface, not a constant. Edit a prompt, prove it
					against the adversarial suite, publish it.
				</p>
			</header>

			<section className="glass-panel space-y-4 p-5">
				<div className="flex items-baseline justify-between">
					<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						Today
					</h2>
					<span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
						{usage.requestsToday} calls · {usage.activeUsersToday} people · {usage.failuresToday}{" "}
						failed
					</span>
				</div>

				<div className="grid gap-4 sm:grid-cols-2">
					<div className="space-y-2">
						<div className="flex items-baseline justify-between text-sm">
							<span className="text-charcoal-blue-600 dark:text-charcoal-blue-300">Tokens</span>
							<span className="tabular-nums text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{usage.tokensToday.toLocaleString()} / {usage.tokenCeiling.toLocaleString()}
							</span>
						</div>
						<Meter used={usage.tokensToday} ceiling={usage.tokenCeiling} />
					</div>

					<div className="space-y-2">
						<div className="flex items-baseline justify-between text-sm">
							<span className="text-charcoal-blue-600 dark:text-charcoal-blue-300">Spend</span>
							<span className="tabular-nums text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{currency(usage.costMicrosToday)} / {currency(usage.costCeilingMicros)}
							</span>
						</div>
						<Meter used={usage.costMicrosToday} ceiling={usage.costCeilingMicros} />
					</div>
				</div>

				{usage.byFeature.length > 0 && (
					<ul className="flex flex-wrap gap-2 pt-1">
						{usage.byFeature.map((f) => (
							<li
								key={f.feature}
								className="rounded-full bg-charcoal-blue-100 px-3 py-1 text-xs text-charcoal-blue-700 dark:bg-white/10 dark:text-charcoal-blue-200"
							>
								{f.feature} · {f.tokens.toLocaleString()} tokens
							</li>
						))}
					</ul>
				)}
			</section>

			<section className="space-y-3">
				<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					Prompts
				</h2>

				<ul className="grid gap-3 sm:grid-cols-2">
					{prompts.map((prompt) => (
						<li key={prompt.key}>
							<Link
								href={`/admin/ai/${encodeURIComponent(prompt.key)}`}
								className="glass-panel block h-full space-y-2 p-5 transition-all hover:-translate-y-0.5 "
							>
								<div className="flex items-center justify-between gap-3">
									<code className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
										{prompt.key}
									</code>
									{prompt.publishedVersion === null ? (
										<span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs text-amber-800 dark:bg-amber-500/15 dark:text-amber-300">
											built-in default
										</span>
									) : (
										<span className="rounded-full bg-verdigris-100 px-2 py-0.5 text-xs text-verdigris-800 dark:bg-verdigris-500/15 dark:text-verdigris-300">
											v{prompt.publishedVersion} live
										</span>
									)}
								</div>
								<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
									{prompt.description}
								</p>
								<p className="text-xs text-charcoal-blue-400 dark:text-charcoal-blue-500">
									{prompt.versionCount} version
									{prompt.versionCount === 1 ? "" : "s"}
									{prompt.draftCount > 0 && ` · ${prompt.draftCount} draft`}
									{prompt.draftCount > 1 && "s"}
								</p>
							</Link>
						</li>
					))}
				</ul>
			</section>
		</div>
	);
}
