import Link from "next/link";
import { Icon } from "@/components/ui/icon";

type Axis = "N" | "W" | "M";

const CLIENTS: { name: string; status: string; stale: boolean; grants: Axis[] }[] = [
	{ name: "Selam T.", status: "Logged today · 6-day streak", stale: false, grants: ["N", "W"] },
	{ name: "Dawit M.", status: "Nothing logged in 4 days", stale: true, grants: ["N", "M"] },
	{ name: "Hanna G.", status: "Logged today · 31-day streak", stale: false, grants: ["N", "W", "M"] },
];

const AXIS_LABEL: Record<string, string> = { N: "Nutrition", W: "Workouts", M: "Measurements" };

export function CoachingSection() {
	return (
		<section aria-labelledby="coaching-heading" className="border-t border-charcoal-blue-200 py-16 sm:py-20">
			<div className="grid grid-cols-1 items-center gap-12 lg:grid-cols-2 lg:gap-16">
				<div className="flex flex-col gap-4">
					<p className="eyebrow">For coaches</p>
					<h2 id="coaching-heading" className="text-[2rem] font-medium leading-tight tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Your clients&rsquo; real week, not a screenshot they remembered to send.
					</h2>
					<p className="text-[15px] leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
						Invite a client, they choose what to share, and you get the log as it happens — plus a chat thread that lives in the same place. Ask the assistant about one client and it answers from their data, billed to you, inside the same consent rules.
					</p>
					<Link href="/register?role=trainer" className="btn-primary mt-1 self-start">
						See the coach view
						<Icon name="arrowRight" size={15} aria-hidden="true" />
					</Link>
				</div>

				<div className="card">
					<div className="flex items-center justify-between border-b border-charcoal-blue-200 px-5 py-3.5 dark:border-charcoal-blue-700">
						<span className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Clients</span>
						<span className="num text-[11.5px] text-charcoal-blue-500 dark:text-charcoal-blue-500">4 active</span>
					</div>
					{CLIENTS.map((client) => (
						<div key={client.name} className="flex items-center gap-3.5 border-b border-charcoal-blue-100 px-5 py-3.5 last:border-b-0 dark:border-charcoal-blue-800">
							<div className="flex-1">
								<div className="text-[13.5px] font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">{client.name}</div>
								<div className={`text-[11.5px] ${client.stale ? "text-burnt-peach-700 dark:text-burnt-peach-400" : "text-charcoal-blue-500 dark:text-charcoal-blue-500"}`}>
									{client.status}
								</div>
							</div>
							<div className="flex gap-1">
								{(["N", "W", "M"] as const).map((axis) => {
									const shared = client.grants.includes(axis);
									return (
										<span
											key={axis}
											title={shared ? AXIS_LABEL[axis] : `${AXIS_LABEL[axis]} not shared`}
											className={`flex h-5 w-5 items-center justify-center text-[10.5px] font-semibold ${
												shared
													? "bg-verdigris-100 text-verdigris-700 dark:bg-verdigris-900 dark:text-verdigris-300"
													: "bg-charcoal-blue-100 text-charcoal-blue-400 dark:bg-charcoal-blue-800 dark:text-charcoal-blue-600"
											}`}
										>
											{axis}
										</span>
									);
								})}
							</div>
						</div>
					))}
				</div>
			</div>
		</section>
	);
}
