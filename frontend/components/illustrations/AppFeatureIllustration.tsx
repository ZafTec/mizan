type IllustrationVariant =
	| "achievements"
	| "dashboard"
	| "meal-plan"
	| "progress"
	| "recipes"
	| "shopping"
	| "trainers"
	| "workouts";

type IllustrationConfig = {
	badge: string;
	title: string;
	subtitle: string;
	accent: string;
	secondary: string;
	third: string;
	listLabel: string;
	insightLabel: string;
	metricLabel: string;
	metricValue: string;
	chartLabel: string;
	chipLabel: string;
	chipValue: string;
	badgeValue: string;
	badgeCaption: string;
	orbitLabel: string;
	calloutValue: string;
	calloutCaption: string;
	coachTitle: string;
	coachCopy: string;
	linePoints: string;
	barHeights: [number, number, number, number];
};

const ILLUSTRATION_CONFIG: Record<IllustrationVariant, IllustrationConfig> = {
	dashboard: {
		badge: "Goal dashboard",
		title: "Macro command center",
		subtitle: "Track meals, trends, coaching, and recovery in one surface.",
		accent: "var(--color-verdigris-500)",
		secondary: "var(--color-sandy-brown-500)",
		third: "var(--color-burnt-peach-500)",
		listLabel: "Live targets",
		insightLabel: "Smart pacing",
		metricLabel: "Protein pace",
		metricValue: "84%",
		chartLabel: "7-day trend",
		chipLabel: "Coach",
		chipValue: "AI online",
		badgeValue: "+18",
		badgeCaption: "streak",
		orbitLabel: "Balance",
		calloutValue: "2190",
		calloutCaption: "kcal logged",
		coachTitle: "Meal plan synced",
		coachCopy: "Macros and trainer notes align for today.",
		linePoints: "0,82 46,58 92,62 138,34 184,42 230,16",
		barHeights: [42, 64, 90, 58],
	},
	progress: {
		badge: "Progress log",
		title: "Daily check-in",
		subtitle: "Capture intake, notes, weight, and momentum without losing context.",
		accent: "var(--color-sandy-brown-500)",
		secondary: "var(--color-verdigris-500)",
		third: "var(--color-burnt-peach-500)",
		listLabel: "Daily totals",
		insightLabel: "Ready to save",
		metricLabel: "Today complete",
		metricValue: "4/5",
		chartLabel: "Entries this week",
		chipLabel: "Notes",
		chipValue: "Captured",
		badgeValue: "+1",
		badgeCaption: "entry",
		orbitLabel: "Consistency",
		calloutValue: "152",
		calloutCaption: "g protein",
		coachTitle: "Progress mapped",
		coachCopy: "New entries feed the dashboard immediately.",
		linePoints: "0,88 46,72 92,54 138,48 184,26 230,12",
		barHeights: [30, 52, 70, 94],
	},
	"meal-plan": {
		badge: "Meal planner",
		title: "Weekly structure",
		subtitle: "Organize recipes, portions, and grocery prep around target macros.",
		accent: "var(--color-verdigris-500)",
		secondary: "var(--color-charcoal-blue-500)",
		third: "var(--color-sandy-brown-500)",
		listLabel: "Plan blocks",
		insightLabel: "Coverage",
		metricLabel: "Meals scheduled",
		metricValue: "21",
		chartLabel: "Week balance",
		chipLabel: "Planner",
		chipValue: "Ready",
		badgeValue: "96%",
		badgeCaption: "filled",
		orbitLabel: "Timing",
		calloutValue: "7",
		calloutCaption: "days mapped",
		coachTitle: "Recipes connected",
		coachCopy: "Shopping and nutrition sync from the same plan.",
		linePoints: "0,72 46,78 92,54 138,40 184,46 230,28",
		barHeights: [60, 82, 74, 88],
	},
	shopping: {
		badge: "Shopping list",
		title: "Prep without friction",
		subtitle: "Turn your plan into a clean, checkable grocery workflow.",
		accent: "var(--color-sandy-brown-500)",
		secondary: "var(--color-verdigris-500)",
		third: "var(--color-charcoal-blue-500)",
		listLabel: "Store run",
		insightLabel: "Auto-generated",
		metricLabel: "Items ready",
		metricValue: "32",
		chartLabel: "Pantry impact",
		chipLabel: "List",
		chipValue: "Shared",
		badgeValue: "12",
		badgeCaption: "checked",
		orbitLabel: "Prep",
		calloutValue: "4",
		calloutCaption: "meals linked",
		coachTitle: "Ingredients grouped",
		coachCopy: "Everything rolls up by meal plan automatically.",
		linePoints: "0,90 46,74 92,70 138,52 184,46 230,30",
		barHeights: [44, 66, 58, 86],
	},
	recipes: {
		badge: "Recipe library",
		title: "Reusable meal building",
		subtitle: "Store favorites, compare macros, and publish clean recipe cards.",
		accent: "var(--color-burnt-peach-500)",
		secondary: "var(--color-verdigris-500)",
		third: "var(--color-sandy-brown-500)",
		listLabel: "Recipe cards",
		insightLabel: "Macros attached",
		metricLabel: "Saved recipes",
		metricValue: "48",
		chartLabel: "Usage trend",
		chipLabel: "Library",
		chipValue: "Growing",
		badgeValue: "5",
		badgeCaption: "new",
		orbitLabel: "Flavor",
		calloutValue: "26",
		calloutCaption: "favorites",
		coachTitle: "Nutrition baked in",
		coachCopy: "Recipe macros stay portable across plans and logs.",
		linePoints: "0,94 46,78 92,68 138,54 184,38 230,18",
		barHeights: [50, 74, 62, 96],
	},
	workouts: {
		badge: "Workout log",
		title: "Training with context",
		subtitle: "Record sessions beside nutrition so performance is not isolated.",
		accent: "var(--color-burnt-peach-500)",
		secondary: "var(--color-charcoal-blue-500)",
		third: "var(--color-verdigris-500)",
		listLabel: "Session blocks",
		insightLabel: "Recovery aware",
		metricLabel: "Sets tracked",
		metricValue: "18",
		chartLabel: "Load trend",
		chipLabel: "Training",
		chipValue: "Synced",
		badgeValue: "+240",
		badgeCaption: "kg moved",
		orbitLabel: "Strength",
		calloutValue: "62",
		calloutCaption: "min session",
		coachTitle: "Workout captured",
		coachCopy: "Training load sits next to your meal and goal data.",
		linePoints: "0,96 46,86 92,62 138,44 184,40 230,24",
		barHeights: [36, 72, 98, 64],
	},
	trainers: {
		badge: "Trainer hub",
		title: "Coaching collaboration",
		subtitle: "Requests, chat, and progress all share the same source of truth.",
		accent: "var(--color-verdigris-500)",
		secondary: "var(--color-charcoal-blue-500)",
		third: "var(--color-sandy-brown-500)",
		listLabel: "Connection queue",
		insightLabel: "Shared context",
		metricLabel: "Active coaches",
		metricValue: "3",
		chartLabel: "Support load",
		chipLabel: "Message",
		chipValue: "Realtime",
		badgeValue: "14",
		badgeCaption: "clients",
		orbitLabel: "Support",
		calloutValue: "24h",
		calloutCaption: "response pace",
		coachTitle: "Trainer matched",
		coachCopy: "Progress, meals, and requests stay aligned in one flow.",
		linePoints: "0,92 46,68 92,64 138,44 184,30 230,20",
		barHeights: [40, 68, 84, 76],
	},
	achievements: {
		badge: "Achievements",
		title: "Momentum rewards",
		subtitle: "Streaks, milestones, and consistency turn into visible progress signals.",
		accent: "var(--color-sandy-brown-500)",
		secondary: "var(--color-burnt-peach-500)",
		third: "var(--color-verdigris-500)",
		listLabel: "Milestones",
		insightLabel: "Progress unlocked",
		metricLabel: "XP earned",
		metricValue: "1280",
		chartLabel: "Streak curve",
		chipLabel: "Rewards",
		chipValue: "Live",
		badgeValue: "+7",
		badgeCaption: "days",
		orbitLabel: "Streak",
		calloutValue: "9",
		calloutCaption: "badges won",
		coachTitle: "Consistency pays",
		coachCopy: "Meal logging and goal hits convert into visible momentum.",
		linePoints: "0,100 46,86 92,72 138,56 184,34 230,12",
		barHeights: [28, 48, 76, 104],
	},
};

export function AppFeatureIllustration({
	variant = "dashboard",
	className,
}: {
	variant?: IllustrationVariant;
	className?: string;
}) {
	const config = ILLUSTRATION_CONFIG[variant];

	return (
		<svg
			viewBox="0 0 640 420"
			className={className}
			role="img"
			aria-label={config.title}
			style={{
				["--scene-accent" as string]: config.accent,
				["--scene-secondary" as string]: config.secondary,
				["--scene-third" as string]: config.third,
			}}
			xmlns="http://www.w3.org/2000/svg"
		>
			<style>{`
				.scene-shell { fill: color-mix(in oklab, var(--card) 96%, transparent); stroke: color-mix(in oklab, var(--border) 84%, transparent); }
				.scene-panel { fill: color-mix(in oklab, var(--card) 94%, transparent); stroke: color-mix(in oklab, var(--border) 88%, transparent); }
				.scene-soft { fill: color-mix(in oklab, var(--background) 70%, var(--card)); }
				.scene-line { stroke: color-mix(in oklab, var(--foreground) 12%, transparent); }
				.scene-text { fill: var(--foreground); }
				.scene-muted { fill: var(--muted-foreground); }
				.scene-accent { fill: var(--scene-accent); }
				.scene-secondary { fill: var(--scene-secondary); }
				.scene-third { fill: var(--scene-third); }
				.scene-accent-soft { fill: color-mix(in oklab, var(--scene-accent) 18%, var(--card)); }
				.scene-secondary-soft { fill: color-mix(in oklab, var(--scene-secondary) 16%, var(--card)); }
				.scene-third-soft { fill: color-mix(in oklab, var(--scene-third) 16%, var(--card)); }
				.scene-outline-accent { stroke: color-mix(in oklab, var(--scene-accent) 75%, transparent); }
				.scene-outline-secondary { stroke: color-mix(in oklab, var(--scene-secondary) 70%, transparent); }
				.scene-outline-third { stroke: color-mix(in oklab, var(--scene-third) 72%, transparent); }
				.float-slow { animation: scene-float-slow 8.4s ease-in-out infinite; transform-origin: center; }
				.float-fast { animation: scene-float-fast 5.8s ease-in-out infinite; transform-origin: center; }
				.pulse-soft { animation: scene-pulse 3.2s ease-in-out infinite; transform-origin: center; }
				.draw-line { stroke-dasharray: 340; stroke-dashoffset: 340; animation: scene-draw 2.8s cubic-bezier(0.16, 1, 0.3, 1) forwards; }
				.bar-grow { transform-box: fill-box; transform-origin: center bottom; animation: scene-grow 1.2s cubic-bezier(0.16, 1, 0.3, 1) both; }
				.bar-grow.delay-1 { animation-delay: 120ms; }
				.bar-grow.delay-2 { animation-delay: 220ms; }
				.bar-grow.delay-3 { animation-delay: 320ms; }
				.orbit { animation: scene-orbit 9.8s linear infinite; transform-origin: 542px 76px; }
				.orbit.reverse { animation-direction: reverse; animation-duration: 7.2s; }
				.spark { animation: scene-blink 2.4s ease-in-out infinite; }
				.spark.delay-1 { animation-delay: 280ms; }
				.spark.delay-2 { animation-delay: 820ms; }
				.spark.delay-3 { animation-delay: 1380ms; }
				@keyframes scene-float-slow {
					0%, 100% { transform: translateY(0px); }
					50% { transform: translateY(-8px); }
				}
				@keyframes scene-float-fast {
					0%, 100% { transform: translateY(0px) translateX(0px); }
					50% { transform: translateY(-10px) translateX(3px); }
				}
				@keyframes scene-pulse {
					0%, 100% { transform: scale(1); opacity: 0.82; }
					50% { transform: scale(1.06); opacity: 1; }
				}
				@keyframes scene-draw {
					to { stroke-dashoffset: 0; }
				}
				@keyframes scene-grow {
					from { transform: scaleY(0.18); opacity: 0.42; }
					to { transform: scaleY(1); opacity: 1; }
				}
				@keyframes scene-orbit {
					from { transform: rotate(0deg) translateX(16px) rotate(0deg); }
					to { transform: rotate(360deg) translateX(16px) rotate(-360deg); }
				}
				@keyframes scene-blink {
					0%, 100% { opacity: 0.3; }
					50% { opacity: 1; }
				}
				@media (prefers-reduced-motion: reduce) {
					.float-slow,
					.float-fast,
					.pulse-soft,
					.draw-line,
					.bar-grow,
					.orbit,
					.spark {
						animation: none !important;
					}
				}
			`}</style>

			<defs>
				<linearGradient id="sceneHeroGlow" x1="122" y1="24" x2="534" y2="386" gradientUnits="userSpaceOnUse">
					<stop offset="0" stopColor="var(--scene-accent)" stopOpacity="0.22" />
					<stop offset="0.52" stopColor="var(--scene-secondary)" stopOpacity="0.16" />
					<stop offset="1" stopColor="var(--scene-third)" stopOpacity="0.2" />
				</linearGradient>
				<linearGradient id="sceneDeviceGradient" x1="233" y1="74" x2="416" y2="336" gradientUnits="userSpaceOnUse">
					<stop offset="0" stopColor="color-mix(in oklab, var(--card) 92%, white)" />
					<stop offset="1" stopColor="color-mix(in oklab, var(--background) 72%, var(--card))" />
				</linearGradient>
				<filter id="sceneShadow" x="-30%" y="-30%" width="160%" height="160%">
					<feDropShadow dx="0" dy="18" stdDeviation="18" floodColor="rgba(15,23,42,0.18)" />
				</filter>
			</defs>

			<rect x="24" y="22" width="592" height="376" rx="34" className="scene-shell" />
			<rect x="24" y="22" width="592" height="376" rx="34" fill="url(#sceneHeroGlow)" opacity="0.62" />
			<circle cx="130" cy="82" r="86" className="pulse-soft scene-accent" opacity="0.08" />
			<circle cx="540" cy="330" r="104" className="pulse-soft scene-secondary" opacity="0.08" />
			<circle cx="504" cy="104" r="72" className="pulse-soft scene-third" opacity="0.08" />

			<g opacity="0.42">
				<path d="M58 74H582" className="scene-line" strokeWidth="1" strokeDasharray="4 8" />
				<path d="M58 344H582" className="scene-line" strokeWidth="1" strokeDasharray="4 8" />
				<path d="M110 52V368" className="scene-line" strokeWidth="1" strokeDasharray="4 10" />
				<path d="M530 52V368" className="scene-line" strokeWidth="1" strokeDasharray="4 10" />
			</g>

			<g className="float-slow" filter="url(#sceneShadow)">
				<rect x="58" y="92" width="146" height="132" rx="24" className="scene-panel" />
				<text x="82" y="122" className="scene-muted" fontSize="13" fontWeight="700" letterSpacing="0.08em">
					{config.listLabel.toUpperCase()}
				</text>
				<g transform="translate(82 140)">
					<rect width="98" height="14" rx="7" className="scene-accent-soft" />
					<rect y="26" width="110" height="12" rx="6" className="scene-secondary-soft" />
					<rect y="50" width="84" height="12" rx="6" className="scene-third-soft" />
					<circle cx="114" cy="7" r="7" className="scene-accent" />
					<circle cx="126" cy="32" r="7" className="scene-secondary" />
					<circle cx="100" cy="56" r="7" className="scene-third" />
				</g>
				<rect x="82" y="186" width="100" height="22" rx="11" className="scene-soft" />
				<text x="98" y="201" className="scene-text" fontSize="11" fontWeight="700">
					{config.insightLabel}
				</text>
			</g>

			<g transform="translate(206 48)" filter="url(#sceneShadow)">
				<rect width="228" height="324" rx="34" fill="url(#sceneDeviceGradient)" stroke="color-mix(in oklab, var(--border) 88%, transparent)" />
				<rect x="72" y="14" width="84" height="10" rx="5" fill="color-mix(in oklab, var(--foreground) 16%, transparent)" />
				<rect x="20" y="34" width="188" height="42" rx="18" className="scene-soft" />
				<text x="38" y="52" className="scene-muted" fontSize="10" fontWeight="700" letterSpacing="0.08em">
					{config.badge.toUpperCase()}
				</text>
				<text x="38" y="68" className="scene-text" fontSize="16" fontWeight="700">
					{config.title}
				</text>
				<text x="20" y="98" className="scene-muted" fontSize="12">
					{config.subtitle}
				</text>

				<g transform="translate(20 120)">
					<rect width="188" height="110" rx="22" className="scene-panel" />
					<text x="18" y="24" className="scene-muted" fontSize="10" fontWeight="700" letterSpacing="0.08em">
						{config.chartLabel.toUpperCase()}
					</text>
					<path d="M18 82H170" className="scene-line" strokeWidth="1.2" strokeDasharray="4 6" />
					<path d="M18 60H170" className="scene-line" strokeWidth="1.2" strokeDasharray="4 6" />
					<path d="M18 38H170" className="scene-line" strokeWidth="1.2" strokeDasharray="4 6" />
					<g transform="translate(22 24)">
						<rect x="0" y={74 - config.barHeights[0]} width="20" height={config.barHeights[0]} rx="10" className="scene-accent bar-grow" />
						<rect x="30" y={74 - config.barHeights[1]} width="20" height={config.barHeights[1]} rx="10" className="scene-secondary bar-grow delay-1" />
						<rect x="60" y={74 - config.barHeights[2]} width="20" height={config.barHeights[2]} rx="10" className="scene-third bar-grow delay-2" />
						<rect x="90" y={74 - config.barHeights[3]} width="20" height={config.barHeights[3]} rx="10" className="scene-accent bar-grow delay-3" opacity="0.85" />
					</g>
					<polyline points={config.linePoints} fill="none" stroke="var(--scene-accent)" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" className="draw-line" />
				</g>

				<g transform="translate(20 246)">
					<rect width="88" height="58" rx="20" className="scene-accent-soft scene-outline-accent" strokeWidth="1.5" />
					<text x="16" y="24" className="scene-muted" fontSize="10" fontWeight="700" letterSpacing="0.08em">
						{config.metricLabel.toUpperCase()}
					</text>
					<text x="16" y="44" className="scene-text" fontSize="22" fontWeight="700">
						{config.metricValue}
					</text>
				</g>

				<g transform="translate(116 246)">
					<rect width="92" height="58" rx="20" className="scene-secondary-soft scene-outline-secondary" strokeWidth="1.5" />
					<text x="16" y="24" className="scene-muted" fontSize="10" fontWeight="700" letterSpacing="0.08em">
						{config.chipLabel.toUpperCase()}
					</text>
					<text x="16" y="44" className="scene-text" fontSize="18" fontWeight="700">
						{config.chipValue}
					</text>
				</g>
			</g>

			<g className="float-fast" filter="url(#sceneShadow)">
				<rect x="466" y="116" width="116" height="152" rx="24" className="scene-panel" />
				<circle cx="524" cy="156" r="26" className="scene-soft" />
				<path d="M524 138l8 12 14 2-10 10 2 14-14-7-14 7 2-14-10-10 14-2z" className="scene-accent" opacity="0.9" />
				<text x="524" y="210" textAnchor="middle" className="scene-text" fontSize="24" fontWeight="700">
					{config.badgeValue}
				</text>
				<text x="524" y="228" textAnchor="middle" className="scene-muted" fontSize="11" fontWeight="700" letterSpacing="0.08em">
					{config.badgeCaption.toUpperCase()}
				</text>
				<rect x="486" y="238" width="76" height="12" rx="6" className="scene-line" />
			</g>

			<g transform="translate(420 286)" className="float-slow" filter="url(#sceneShadow)">
				<rect width="164" height="78" rx="24" className="scene-panel" />
				<circle cx="26" cy="26" r="12" className="scene-accent-soft" />
				<path d="M20 30c4-7 12-10 16-8 4 2 4 10-2 14-6 4-14 0-14-6z" className="scene-accent" />
				<text x="48" y="28" className="scene-text" fontSize="13" fontWeight="700">
					{config.coachTitle}
				</text>
				<text x="48" y="47" className="scene-muted" fontSize="11">
					{config.coachCopy}
				</text>
				<text x="18" y="64" className="scene-secondary" fontSize="24" fontWeight="700">
					{config.calloutValue}
				</text>
				<text x="74" y="64" className="scene-muted" fontSize="11" fontWeight="700" letterSpacing="0.08em">
					{config.calloutCaption.toUpperCase()}
				</text>
			</g>

			<g transform="translate(70 276)" className="float-fast" filter="url(#sceneShadow)">
				<rect width="132" height="86" rx="24" className="scene-panel" />
				<circle cx="34" cy="34" r="18" className="scene-third-soft" />
				<circle cx="34" cy="34" r="10" fill="none" className="scene-outline-third" strokeWidth="5" strokeDasharray="48 14" />
				<text x="64" y="32" className="scene-text" fontSize="18" fontWeight="700">
					{config.orbitLabel}
				</text>
				<text x="64" y="50" className="scene-muted" fontSize="11">
					Cross-system visibility
				</text>
			</g>

			<g transform="translate(542 76)">
				<circle r="22" className="scene-soft" opacity="0.86" />
				<circle r="16" fill="none" className="scene-outline-accent" strokeWidth="1.5" strokeDasharray="4 6" />
				<circle r="3.5" className="scene-accent orbit" />
				<circle r="3.5" className="scene-secondary orbit reverse" />
			</g>

			<circle cx="88" cy="72" r="4" className="scene-accent spark" />
			<circle cx="124" cy="54" r="3" className="scene-third spark delay-1" />
			<circle cx="578" cy="102" r="4" className="scene-secondary spark delay-2" />
			<circle cx="560" cy="346" r="3" className="scene-accent spark delay-3" />
		</svg>
	);
}
