"use client";

import type { HTMLAttributes } from "react";
import {
	ActivityIcon,
	ArrowRightIcon,
	BadgeAlertIcon,
	BellIcon,
	BotIcon,
	BrainIcon,
	CalendarCheckIcon,
	CartIcon,
	ChartLineIcon,
	CircleCheckIcon,
	CookingPotIcon,
	FlameIcon,
	GithubIcon,
	HeartIcon,
	HomeIcon,
	LockIcon,
	LogoutIcon,
	MenuIcon,
	MessageCircleIcon,
	MoonIcon,
	RocketIcon,
	SearchIcon,
	SettingsIcon,
	ShieldCheckIcon,
	SparklesIcon,
	SunIcon,
	TrendingUpIcon,
	TwitterIcon,
	UploadIcon,
	UserIcon,
	UsersIcon,
	XIcon,
	ZapIcon,
} from "lucide-animated";
import { cn } from "@/lib/utils";

const iconMap = {
	activity: ActivityIcon,
	arrowRight: ArrowRightIcon,
	badgeAlert: BadgeAlertIcon,
	bell: BellIcon,
	bot: BotIcon,
	brain: BrainIcon,
	calendarCheck: CalendarCheckIcon,
	cart: CartIcon,
	chartLine: ChartLineIcon,
	circleCheck: CircleCheckIcon,
	cookingPot: CookingPotIcon,
	flame: FlameIcon,
	github: GithubIcon,
	heart: HeartIcon,
	home: HomeIcon,
	lock: LockIcon,
	logout: LogoutIcon,
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
	twitter: TwitterIcon,
	upload: UploadIcon,
	user: UserIcon,
	users: UsersIcon,
	x: XIcon,
	zap: ZapIcon,
} as const;

export type AnimatedIconName = keyof typeof iconMap;

interface AnimatedIconProps extends HTMLAttributes<HTMLDivElement> {
	name: AnimatedIconName;
	size?: number;
}

export function AnimatedIcon({ name, size = 20, className, ...props }: AnimatedIconProps) {
	const Icon = iconMap[name];

	return <Icon size={size} className={cn("shrink-0 text-current", className)} {...props} />;
}
