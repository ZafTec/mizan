import Link from "next/link";
import { Icon } from "@/components/ui/icon";

export function CTASection() {
	return (
		<section
			data-testid="cta-section"
			aria-labelledby="final-cta-heading"
			className="flex flex-col items-center gap-5 py-20 text-center sm:py-24"
		>
			<h2 id="final-cta-heading" className="max-w-[20ch] text-4xl font-medium leading-tight tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-5xl">
				Log tonight&rsquo;s dinner in ten seconds.
			</h2>
			<p className="max-w-lg text-[15.5px] leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
				Then do it again tomorrow. That is the whole product, and everything else here exists to make that second day easier than the first.
			</p>
			<Link href="/register" className="btn-primary mt-1">
				Start logging free
				<Icon name="arrowRight" size={16} aria-hidden="true" />
			</Link>
		</section>
	);
}
