import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { AppFeatureIllustration } from "@/components/illustrations/AppFeatureIllustration";
import { ProducerBadge } from "./ProducerBadge";

export function HeroSection() {
	return (
		<section
			data-testid="hero-section"
			aria-labelledby="hero-heading"
			className="surface-panel relative overflow-hidden rounded-[34px] border-charcoal-blue-900 bg-charcoal-blue-900 p-8 sm:p-10 lg:p-12 dark:border-white/10 dark:bg-charcoal-blue-950"
		>
			<div className="absolute inset-0 bg-[url('/grid.svg')] opacity-10" aria-hidden="true" />
			<div className="absolute right-0 top-0 h-96 w-96 rounded-full bg-brand-500/10 blur-3xl" aria-hidden="true" />
			<div className="absolute bottom-0 left-0 h-64 w-64 rounded-full bg-white/5 blur-3xl" aria-hidden="true" />

			<div className="pointer-events-none absolute right-0 top-1/2 hidden w-[28rem] -translate-y-1/2 opacity-95 drop-shadow-2xl lg:block xl:right-10 2xl:right-20">
				<AppFeatureIllustration variant="dashboard" priority />
			</div>

			<div className="relative z-10 max-w-3xl">
				<ProducerBadge className="mb-6" />
				<h1
					id="hero-heading"
					className="max-w-2xl text-4xl font-semibold tracking-tight text-white sm:text-5xl lg:text-6xl"
				>
					Find your <span className="text-brand-300">balance</span> across food, training, and progress.
				</h1>
				<p className="mt-5 max-w-2xl text-lg leading-relaxed text-white/85 sm:text-xl">
					Mizan is the single workspace for your meals, workouts, and coaching. Log fast, plan a week, ship goals.
				</p>
				<div className="mt-8 flex flex-col gap-4 sm:flex-row">
					<Link
						href="/register"
						className="btn-secondary btn-lg border-white/20 bg-white/95 text-brand-600 hover:bg-white dark:bg-white/95 dark:text-brand-600 dark:hover:bg-white"
					>
						Get started free
						<AnimatedIcon name="arrowRight" size={18} aria-hidden="true" />
					</Link>
					<Link
						href="#pricing"
						className="btn-lg inline-flex items-center justify-center gap-2 rounded-2xl border border-white/20 bg-white/10 px-6 py-3.5 font-semibold text-white backdrop-blur-sm transition-colors duration-200 ease-out hover:bg-white/20 active:scale-[0.97] dark:border-white/10 dark:bg-white/10 dark:hover:bg-white/20"
					>
						See pricing
					</Link>
				</div>

				<dl className="mt-10 grid max-w-xl grid-cols-3 gap-4 text-white/85">
					{[
						{ label: "meals logged by users", value: "2M+" },
						{ label: "average log time", value: "6s" },
						{ label: "cancel anytime", value: "14-day" },
					].map((stat, index) => (
						<div
							key={stat.label}
							className="stagger-item rounded-2xl border border-white/15 bg-white/5 px-4 py-3 backdrop-blur-sm"
							style={{ animationDelay: `${index * 60}ms` }}
						>
							<dt className="text-xs uppercase tracking-[0.14em] text-white/60">
								{stat.label}
							</dt>
							<dd className="mt-1 text-lg font-semibold text-white sm:text-xl">
								{stat.value}
							</dd>
						</div>
					))}
				</dl>
			</div>
		</section>
	);
}
