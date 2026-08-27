import type { SVGProps } from "react";
import {
	ActivityIcon,
	ArrowRightIcon,
	BadgeAlertIcon,
	BellIcon,
	BotIcon,
	BrainIcon,
	CalendarCheckIcon,
	ChartLineIcon,
	CircleCheckIcon,
	CookingPotIcon,
	FlameIcon,
	HeartIcon,
	HomeIcon,
	LockIcon,
	LogOutIcon,
	MenuIcon,
	MessageCircleIcon,
	MoonIcon,
	RocketIcon,
	SearchIcon,
	SettingsIcon,
	ShieldCheckIcon,
	ShoppingCartIcon,
	SparklesIcon,
	SunIcon,
	TrendingUpIcon,
	UploadIcon,
	UserIcon,
	UsersIcon,
	XIcon,
	ZapIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Static line icons at a consistent 1.7 stroke, matching the design spec's
 * hairline weight. These were animated (`lucide-animated`) - every icon ran a
 * loop on hover or mount, which on a dense page is a screen full of things
 * moving for no reason. An icon labels; it does not perform.
 *
 * Brand marks are inlined below because lucide dropped them: it no longer
 * ships Github or Twitter.
 */
function GithubMark(props: SVGProps<SVGSVGElement>) {
	return (
		<svg viewBox="0 0 24 24" fill="currentColor" {...props}>
			<path d="M12 .5C5.73.5.5 5.73.5 12c0 5.08 3.29 9.39 7.86 10.91.58.11.79-.25.79-.56 0-.28-.01-1.02-.02-2-3.2.69-3.88-1.54-3.88-1.54-.52-1.33-1.28-1.68-1.28-1.68-1.05-.72.08-.7.08-.7 1.16.08 1.77 1.19 1.77 1.19 1.03 1.766 2.7 1.256 3.36.96.1-.75.4-1.26.73-1.55-2.55-.29-5.24-1.28-5.24-5.69 0-1.26.45-2.29 1.19-3.1-.12-.29-.52-1.46.11-3.05 0 0 .97-.31 3.18 1.18a11 11 0 0 1 5.8 0c2.2-1.49 3.17-1.18 3.17-1.18.63 1.59.23 2.76.12 3.05.74.81 1.18 1.84 1.18 3.1 0 4.42-2.69 5.39-5.25 5.68.41.36.78 1.06.78 2.14 0 1.55-.02 2.8-.02 3.18 0 .31.21.68.8.56A11.51 11.51 0 0 0 23.5 12C23.5 5.73 18.27.5 12 .5Z" />
		</svg>
	);
}

function TwitterMark(props: SVGProps<SVGSVGElement>) {
	return (
		<svg viewBox="0 0 24 24" fill="currentColor" {...props}>
			<path d="M18.24 2.25h3.31l-7.23 8.26 8.5 11.24h-6.65l-5.22-6.82-5.97 6.82H1.66l7.73-8.84L1.25 2.25h6.82l4.71 6.23 5.46-6.23Zm-1.16 17.52h1.83L7.08 4.13H5.11l11.97 15.64Z" />
		</svg>
	);
}

const iconMap = {
	activity: ActivityIcon,
	arrowRight: ArrowRightIcon,
	badgeAlert: BadgeAlertIcon,
	bell: BellIcon,
	bot: BotIcon,
	brain: BrainIcon,
	calendarCheck: CalendarCheckIcon,
	cart: ShoppingCartIcon,
	chartLine: ChartLineIcon,
	circleCheck: CircleCheckIcon,
	cookingPot: CookingPotIcon,
	flame: FlameIcon,
	github: GithubMark,
	heart: HeartIcon,
	home: HomeIcon,
	lock: LockIcon,
	logout: LogOutIcon,
	menu: MenuIcon,
	messageCircle: MessageCircleIcon,
	moon: MoonIcon,
	rocket: RocketIcon,
	search: SearchIcon,
	settings: SettingsIcon,
	shieldCheck: ShieldCheckIcon,
	sparkles: SparklesIcon,
	sun: SunIcon,
	trendingUp: TrendingUpIcon,
	twitter: TwitterMark,
	upload: UploadIcon,
	user: UserIcon,
	users: UsersIcon,
	x: XIcon,
	zap: ZapIcon,
} as const;

export type IconName = keyof typeof iconMap;

interface IconProps extends Omit<SVGProps<SVGSVGElement>, "name"> {
	name: IconName;
	size?: number;
}

export function Icon({ name, size = 18, className, ...props }: IconProps) {
	const Glyph = iconMap[name];

	return (
		<Glyph
			width={size}
			height={size}
			strokeWidth={1.7}
			className={cn("shrink-0 text-current", className)}
			{...props}
		/>
	);
}
