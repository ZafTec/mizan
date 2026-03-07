"use client";

import type { HTMLAttributes } from "react";
import {
	ActivityIcon,
	ArrowRightIcon,
	BadgeAlertIcon,
	BotIcon,
	BrainIcon,
	CalendarCheckIcon,
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
	ShieldCheckIcon,
	SparklesIcon,
	SunIcon,
	TrendingUpIcon,
	TwitterIcon,
	UploadIcon,
	UserIcon,
	UsersIcon,
	XIcon,
} from "lucide-animated";
import { cn } from "@/lib/utils";

const iconMap = {
	activity: ActivityIcon,
	arrowRight: ArrowRightIcon,
	badgeAlert: BadgeAlertIcon,
	bot: BotIcon,
	brain: BrainIcon,
	calendarCheck: CalendarCheckIcon,
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
	shieldCheck: ShieldCheckIcon,
	sparkles: SparklesIcon,
	sun: SunIcon,
	trendingUp: TrendingUpIcon,
	twitter: TwitterIcon,
	upload: UploadIcon,
	user: UserIcon,
	users: UsersIcon,
	x: XIcon,
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
