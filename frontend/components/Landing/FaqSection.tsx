const QUESTIONS = [
	{
		q: "Does the AI read my log?",
		a: "Only what you switch on, per axis. Nutrition, training and body are three separate consents, all off by default. Turn one off and the next question is answered without it — nothing is retained from the last one.",
	},
	{
		q: "What can my coach see?",
		a: "Exactly the axes you granted, and you can end it in one click. Not even an administrator can widen a coach's access — those switches belong to you, and there is no endpoint that lets anyone else touch them.",
	},
	{
		q: "Will my streak lie to me?",
		a: "It runs on your timezone, not the server's, and the app tells you the exact hour it resets. Miss a day and an earned freeze covers you — you get one a week, up to two. No silent resets at midnight UTC.",
	},
	{
		q: "Can I take it with me?",
		a: "Export everything you have logged, any time, as one file. Or skip the hosted version entirely: it is a Docker Compose up on your own machine, with the same database schema.",
	},
];

export function FaqSection() {
	return (
		<section aria-labelledby="faq-heading" className="grid grid-cols-1 gap-10 border-t border-charcoal-blue-200 py-16 sm:py-20 lg:grid-cols-[0.8fr_1.2fr] lg:gap-16">
			<div className="flex flex-col gap-3.5">
				<p className="eyebrow">The parts people actually ask about</p>
				<h2 id="faq-heading" className="text-3xl font-medium leading-tight tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					The awkward questions, answered first.
				</h2>
				<p className="text-sm leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
					Every one of these is a rule in the code, not a policy page.
				</p>
			</div>

			<dl className="flex flex-col">
				{QUESTIONS.map((item) => (
					<div key={item.q} className="grid grid-cols-1 gap-2 border-t border-charcoal-blue-200 py-6 last:border-b sm:grid-cols-[200px_1fr] sm:gap-7 dark:border-charcoal-blue-700">
						<dt className="text-lg font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
							{item.q}
						</dt>
						<dd className="text-sm leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">{item.a}</dd>
					</div>
				))}
			</dl>
		</section>
	);
}
