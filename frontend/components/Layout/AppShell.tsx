"use client";

import Link from "next/link";
import Image from "next/image";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { ChevronDown } from "lucide-react";
import type { User } from "@/lib/auth";
import { signOut } from "@/lib/auth-client";
import { appToast } from "@/lib/toast";
import { clearAppearanceCookie } from "@/lib/appearance-cookie";
import { AnimatedIcon, type AnimatedIconName } from "@/components/ui/animated-icon";
import { cn } from "@/lib/utils";
import logoTransparent from "@/public/logo_transparent.png";

type NavItem = {
	href: string;
	label: string;
	icon: AnimatedIconName;
	badge?: number;
	adminOnly?: boolean;
};

const PRIMARY_NAV: NavItem[] = [
	{ href: "/dashboard", label: "Dashboard", icon: "home" },
	{ href: "/meals", label: "Meals", icon: "flame" },
	{ href: "/recipes", label: "Recipes", icon: "cookingPot" },
	{ href: "/meal-plan", label: "Meal Plan", icon: "calendarCheck" },
	{ href: "/ingredients", label: "Foods", icon: "search" },
	{ href: "/workouts", label: "Workouts", icon: "activity" },
	{ href: "/exercises", label: "Exercises", icon: "zap" },
	{ href: "/body-measurements", label: "Body", icon: "chartLine" },
	{ href: "/goal", label: "Goals", icon: "rocket" },
	{ href: "/achievements", label: "Achievements", icon: "sparkles" },
	{ href: "/habits", label: "Habits", icon: "circleCheck" },
	{ href: "/ai", label: "AI Coach", icon: "brain" },
	{ href: "/messaging", label: "Messages", icon: "messageCircle" },
	{ href: "/community", label: "Community", icon: "users" },
	{ href: "/trainers", label: "Trainers", icon: "heart" },
];

const SECONDARY_NAV: NavItem[] = [
	{ href: "/notifications", label: "Notifications", icon: "bell" },
	{ href: "/profile", label: "Profile", icon: "user" },
	{ href: "/profile/settings", label: "Settings", icon: "settings" },
	{ href: "/admin", label: "Admin", icon: "shieldCheck", adminOnly: true },
];

const BOTTOM_NAV: NavItem[] = [
	{ href: "/dashboard", label: "Home", icon: "home" },
	{ href: "/meals", label: "Meals", icon: "flame" },
	{ href: "/workouts", label: "Train", icon: "activity" },
	{ href: "/ai", label: "AI", icon: "brain" },
	{ href: "/profile", label: "Me", icon: "user" },
];

function isActive(pathname: string | null, href: string) {
	if (!pathname) return false;
	if (href === "/dashboard") return pathname === "/dashboard";
	return pathname === href || pathname.startsWith(`${href}/`);
}

function UserAvatar({ user, size = 36 }: { user: User; size?: number }) {
	if (user.image) {
		return (
			<div
				className="relative overflow-hidden rounded-2xl ring-1 ring-brand-500/15"
				style={{ width: size, height: size }}
			>
				<Image
					src={user.image}
					alt={user.name || user.email || "User"}
					fill
					sizes={`${size}px`}
					className="object-cover"
				/>
			</div>
		);
	}
	return (
		<div
			className="flex items-center justify-center rounded-2xl bg-brand-600 font-semibold text-white ring-1 ring-brand-500/15 dark:bg-brand-500"
			style={{ width: size, height: size, fontSize: size * 0.38 }}
		>
			{user.email?.charAt(0).toUpperCase() || "U"}
		</div>
	);
}

function SidebarLink({ item, collapsed }: { item: NavItem; collapsed: boolean }) {
	const pathname = usePathname();
	const active = isActive(pathname, item.href);
	return (
		<Link
			href={item.href}
			className={cn(
				"press-feedback group relative flex items-center gap-3 rounded-2xl px-3 py-2.5 text-sm font-medium transition-[background-color,color,box-shadow] duration-[160ms] ease-[cubic-bezier(0.23,1,0.32,1)]",
				active
					? "bg-brand-600 text-white shadow-lg shadow-brand-500/25 dark:bg-brand-500 dark:text-charcoal-blue-950"
					: "text-charcoal-blue-600 hover:bg-white/70 hover:text-charcoal-blue-900 dark:text-charcoal-blue-200 dark:hover:bg-white/5 dark:hover:text-charcoal-blue-50",
				collapsed && "justify-center px-2"
			)}
			title={collapsed ? item.label : undefined}
		>
			<span
				className={cn(
					"flex h-5 w-5 shrink-0 items-center justify-center",
					active ? "text-white dark:text-charcoal-blue-950" : "text-charcoal-blue-500 group-hover:text-current dark:text-charcoal-blue-300"
				)}
			>
				<AnimatedIcon name={item.icon} size={18} aria-hidden="true" />
			</span>
			{!collapsed && <span className="truncate">{item.label}</span>}
			{!collapsed && item.badge ? (
				<span className="ml-auto inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-burnt-peach-500 px-1.5 text-[10px] font-semibold text-white">
					{item.badge}
				</span>
			) : null}
		</Link>
	);
}

function BottomNavLink({ item }: { item: NavItem }) {
	const pathname = usePathname();
	const active = isActive(pathname, item.href);
	return (
		<Link
			href={item.href}
			className={cn(
				"flex flex-1 flex-col items-center justify-center gap-1 py-2 text-[10px] font-medium transition-colors",
				active
					? "text-brand-700 dark:text-brand-300"
					: "text-charcoal-blue-500 hover:text-charcoal-blue-900 dark:text-charcoal-blue-400 dark:hover:text-white"
			)}
		>
			<span
				className={cn(
					"flex h-7 w-7 items-center justify-center rounded-2xl transition-all",
					active && "bg-brand-600 text-white shadow-md shadow-brand-500/30 dark:bg-brand-500 dark:text-charcoal-blue-950"
				)}
			>
				<AnimatedIcon name={item.icon} size={16} aria-hidden="true" />
			</span>
			<span className="truncate">{item.label}</span>
		</Link>
	);
}

export interface AppShellProps {
	user: User;
	children: React.ReactNode;
	variant?: "dashboard" | "admin";
}

export default function AppShell({ user, children, variant = "dashboard" }: AppShellProps) {
	const router = useRouter();
	const pathname = usePathname();
	const [collapsed, setCollapsed] = useState(false);
	const [userMenuOpen, setUserMenuOpen] = useState(false);
	const [mobileSheetOpen, setMobileSheetOpen] = useState(false);
	const [showLogoutModal, setShowLogoutModal] = useState(false);
	const userMenuRef = useRef<HTMLDivElement>(null);
	const mobileSheetRef = useRef<HTMLDivElement>(null);

	const isAdmin = user.role === "admin";
	const visiblePrimary = PRIMARY_NAV;
	const visibleSecondary = SECONDARY_NAV.filter((item) => !item.adminOnly || isAdmin);

	// Menus close via their own onClick handlers, not via a pathname-tracking effect.

	useEffect(() => {
		function onClick(event: MouseEvent) {
			const target = event.target as HTMLElement;
			if (userMenuOpen && userMenuRef.current && !userMenuRef.current.contains(target)) {
				if (!target.closest("[data-app-shell-user-trigger]")) {
					setUserMenuOpen(false);
				}
			}
			if (mobileSheetOpen && mobileSheetRef.current && !mobileSheetRef.current.contains(target)) {
				if (!target.closest("[data-app-shell-mobile-trigger]")) {
					setMobileSheetOpen(false);
				}
			}
		}
		function onEscape(event: KeyboardEvent) {
			if (event.key === "Escape") {
				setUserMenuOpen(false);
				setMobileSheetOpen(false);
			}
		}
		document.addEventListener("mousedown", onClick);
		document.addEventListener("keydown", onEscape);
		return () => {
			document.removeEventListener("mousedown", onClick);
			document.removeEventListener("keydown", onEscape);
		};
	}, [userMenuOpen, mobileSheetOpen]);

	useEffect(() => {
		document.body.style.overflow = mobileSheetOpen ? "hidden" : "";
		return () => {
			document.body.style.overflow = "";
		};
	}, [mobileSheetOpen]);

	async function handleLogout() {
		try {
			clearAppearanceCookie();
			await signOut({
				fetchOptions: {
					onSuccess: () => {
						router.push("/");
						router.refresh();
					},
				},
			});
		} catch (error) {
			appToast.error(error, "Failed to sign out");
		}
	}

	const roleLabel = user.role && user.role !== "user" ? user.role : null;

	return (
		<div className="shell-fullbleed relative flex min-h-screen overflow-x-clip bg-[color-mix(in_oklab,var(--color-charcoal-blue-50)_92%,white)] dark:bg-[color-mix(in_oklab,var(--color-charcoal-blue-950)_92%,black)]">
			{/* Soft decorative blobs (v2 aesthetic). Kept inside the shell so the body
				never scrolls to reveal them; html/body overflow-x: clip is the backstop. */}
			<div aria-hidden="true" className="pointer-events-none absolute right-[-5%] top-[-10%] h-[500px] w-[500px] rounded-full bg-verdigris-200/30 blur-[120px] -z-10" />
			<div aria-hidden="true" className="pointer-events-none absolute bottom-[-10%] left-[-5%] h-[400px] w-[400px] rounded-full bg-sandy-brown-200/25 blur-[100px] -z-10" />

			{/* Desktop Sidebar */}
			<aside
				className={cn(
					"sticky top-0 hidden h-screen shrink-0 flex-col border-r border-charcoal-blue-200/70 bg-white/85 backdrop-blur-xl dark:border-white/10 dark:bg-charcoal-blue-950/80 lg:flex",
					collapsed ? "w-20" : "w-72"
				)}
			>
				<div className={cn("flex shrink-0 items-center gap-3 border-b border-charcoal-blue-200/70 px-4 py-5 dark:border-white/10", collapsed && "flex-col gap-2 px-2")}>
					<Link href="/dashboard" className={cn("flex items-center gap-3", collapsed && "flex-col gap-1")}>
						<div className="relative h-11 w-11 shrink-0 overflow-hidden rounded-2xl ring-1 ring-brand-500/20">
							<Image src={logoTransparent} alt="Mizan" fill sizes="44px" className="object-cover" priority />
						</div>
						{!collapsed && (
							<div className="flex flex-col leading-tight">
								<span className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Mizan</span>
								<span className="text-[10px] uppercase tracking-[0.18em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
									{variant === "admin" ? "Admin" : "Balance"}
								</span>
							</div>
						)}
					</Link>
					<button
						type="button"
						onClick={() => setCollapsed((c) => !c)}
						className={cn(
							"flex h-8 w-8 items-center justify-center rounded-xl border border-charcoal-blue-200 text-charcoal-blue-500 transition-colors hover:bg-charcoal-blue-50 hover:text-charcoal-blue-900 dark:border-white/10 dark:text-charcoal-blue-300 dark:hover:bg-white/5 dark:hover:text-white",
							collapsed ? "mx-auto" : "ml-auto"
						)}
						aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
						title={collapsed ? "Expand" : "Collapse"}
					>
						<ChevronDown className={cn("h-4 w-4 transition-transform", collapsed ? "rotate-90" : "-rotate-90")} />
					</button>
				</div>

				<nav className={cn("custom-scrollbar flex-1 space-y-1 overflow-y-auto px-3 py-4", collapsed && "px-2")}>
					{!collapsed && (
						<p className="px-2 pb-2 pt-1 text-[10px] font-semibold uppercase tracking-[0.18em] text-charcoal-blue-400 dark:text-charcoal-blue-400">
							Main
						</p>
					)}
					{visiblePrimary.map((item) => (
						<SidebarLink key={item.href} item={item} collapsed={collapsed} />
					))}
					<div className="my-3 border-t border-charcoal-blue-200/60 dark:border-white/5" />
					{!collapsed && (
						<p className="px-2 pb-2 pt-1 text-[10px] font-semibold uppercase tracking-[0.18em] text-charcoal-blue-400 dark:text-charcoal-blue-400">
							Account
						</p>
					)}
					{visibleSecondary.map((item) => (
						<SidebarLink key={item.href} item={item} collapsed={collapsed} />
					))}
				</nav>

				<div className={cn("shrink-0 border-t border-charcoal-blue-200/70 p-3 dark:border-white/10", collapsed && "px-2")}>
					{collapsed ? (
						<button
							type="button"
							onClick={() => setShowLogoutModal(true)}
							className="flex h-10 w-full items-center justify-center rounded-2xl text-charcoal-blue-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10"
							aria-label="Sign out"
							title="Sign out"
						>
							<AnimatedIcon name="logout" size={16} />
						</button>
					) : (
						<div className="flex items-center gap-3 rounded-2xl border border-charcoal-blue-200 bg-white/80 p-2.5 dark:border-white/10 dark:bg-charcoal-blue-950/60">
							<UserAvatar user={user} size={36} />
							<div className="min-w-0 flex-1 leading-tight">
								<p className="truncate text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
									{user.name || user.email}
								</p>
								{roleLabel && (
									<p className="truncate text-[11px] text-charcoal-blue-500 dark:text-charcoal-blue-400">
										{roleLabel}
									</p>
								)}
							</div>
							<button
								type="button"
								onClick={() => setShowLogoutModal(true)}
								className="flex h-8 w-8 items-center justify-center rounded-xl text-charcoal-blue-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10"
								aria-label="Sign out"
							>
								<AnimatedIcon name="logout" size={16} />
							</button>
						</div>
					)}
				</div>
			</aside>

			{/* Main column */}
			<div className="flex min-w-0 flex-1 flex-col">
				{/* Top bar */}
				<header className="sticky top-0 z-40 border-b border-charcoal-blue-200/70 bg-white/80 backdrop-blur-xl dark:border-white/10 dark:bg-charcoal-blue-950/75">
					<div className="flex h-16 items-center gap-3 px-4 sm:px-6 lg:px-8">
						{/* Mobile menu */}
						<button
							type="button"
							data-app-shell-mobile-trigger
							onClick={() => setMobileSheetOpen((o) => !o)}
							className="flex h-10 w-10 items-center justify-center rounded-2xl border border-charcoal-blue-200 text-charcoal-blue-600 hover:text-charcoal-blue-900 dark:border-white/10 dark:text-charcoal-blue-200 dark:hover:text-white lg:hidden"
							aria-label="Toggle menu"
							aria-expanded={mobileSheetOpen}
						>
							<AnimatedIcon name={mobileSheetOpen ? "x" : "menu"} size={18} />
						</button>

						{/* Mobile logo */}
						<Link href="/dashboard" className="flex items-center gap-2 lg:hidden">
							<div className="relative h-9 w-9 shrink-0 overflow-hidden rounded-xl ring-1 ring-brand-500/20">
								<Image src={logoTransparent} alt="Mizan" fill sizes="36px" className="object-cover" />
							</div>
							<span className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Mizan</span>
						</Link>

						{/* Desktop search */}
						<div className="relative ml-0 hidden flex-1 max-w-md lg:block">
							<input
								type="search"
								placeholder="Search foods, recipes, workouts…"
								className="h-10 w-full rounded-2xl border border-charcoal-blue-200 bg-white/80 pl-10 pr-4 text-sm text-charcoal-blue-900 placeholder-charcoal-blue-400 outline-none backdrop-blur-xl transition-colors focus:border-verdigris-500 focus:ring-4 focus:ring-verdigris-300/20 dark:border-white/10 dark:bg-charcoal-blue-950/60 dark:text-charcoal-blue-50 dark:placeholder-charcoal-blue-400"
							/>
							<span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-charcoal-blue-400">
								<AnimatedIcon name="search" size={16} />
							</span>
						</div>

						<div className="ml-auto flex items-center gap-2">
							<Link
								href="/notifications"
								className="relative flex h-10 w-10 items-center justify-center rounded-2xl border border-charcoal-blue-200 text-charcoal-blue-600 transition-colors hover:text-charcoal-blue-900 dark:border-white/10 dark:text-charcoal-blue-200 dark:hover:text-white"
								aria-label="Notifications"
							>
								<AnimatedIcon name="bell" size={18} />
							</Link>
							<div className="relative">
								<button
									type="button"
									data-app-shell-user-trigger
									onClick={() => setUserMenuOpen((o) => !o)}
									className="flex items-center gap-2 rounded-full border border-charcoal-blue-200 bg-white/80 px-2 py-1.5 text-sm text-charcoal-blue-700 transition-colors hover:border-charcoal-blue-300 dark:border-white/10 dark:bg-charcoal-blue-950/60 dark:text-charcoal-blue-200"
									aria-expanded={userMenuOpen}
									aria-haspopup="menu"
								>
									<UserAvatar user={user} size={30} />
									<span className="hidden max-w-[10rem] truncate font-medium text-charcoal-blue-900 sm:inline dark:text-charcoal-blue-50">
										{user.name || user.email?.split("@")[0]}
									</span>
									<ChevronDown
										className={cn(
											"h-4 w-4 text-charcoal-blue-400 transition-transform",
											userMenuOpen && "rotate-180"
										)}
									/>
								</button>
								{userMenuOpen && (
									<div
										ref={userMenuRef}
										className="menu-pop absolute right-0 top-[calc(100%+0.5rem)] z-50 w-60 overflow-hidden rounded-[24px] border border-charcoal-blue-200 bg-white p-1.5 shadow-2xl shadow-charcoal-blue-950/15 dark:border-white/10 dark:bg-charcoal-blue-950"
									>
										<div className="mb-1 rounded-2xl px-3 py-2.5">
											<p className="truncate text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
												{user.name || user.email}
											</p>
											<p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
												{user.email}
											</p>
										</div>
										<div className="space-y-1">
											{visibleSecondary.map((item) => (
												<Link
													key={item.href}
													href={item.href}
													onClick={() => setUserMenuOpen(false)}
													className="flex items-center gap-3 rounded-2xl px-3 py-2 text-sm text-charcoal-blue-700 hover:bg-charcoal-blue-50 dark:text-charcoal-blue-200 dark:hover:bg-white/5"
												>
													<span className="icon-chip h-8 w-8">
														<AnimatedIcon name={item.icon} size={14} />
													</span>
													{item.label}
												</Link>
											))}
										</div>
										<div className="mt-1 border-t border-charcoal-blue-200/70 pt-1.5 dark:border-white/10">
											<button
												type="button"
												onClick={() => {
													setUserMenuOpen(false);
													setShowLogoutModal(true);
												}}
												className="flex w-full items-center gap-3 rounded-2xl px-3 py-2 text-sm font-medium text-red-600 transition-colors hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-500/10"
											>
												<span className="flex h-8 w-8 items-center justify-center rounded-2xl border border-red-200 text-red-500 dark:border-red-500/20 dark:text-red-400">
													<AnimatedIcon name="logout" size={14} />
												</span>
												Sign out
											</button>
										</div>
									</div>
								)}
							</div>
						</div>
					</div>
				</header>

				{/* Mobile slide-out sheet */}
				{mobileSheetOpen && (
					<>
						<div
							className="fixed inset-0 z-40 bg-charcoal-blue-950/40 backdrop-blur-[2px] lg:hidden"
							onClick={() => setMobileSheetOpen(false)}
						/>
						<aside
							ref={mobileSheetRef}
							className="mobile-sheet-enter fixed inset-y-0 left-0 z-50 flex w-80 max-w-[85vw] flex-col border-r border-charcoal-blue-200 bg-white shadow-2xl dark:border-white/10 dark:bg-charcoal-blue-950 lg:hidden"
						>
							<div className="flex shrink-0 items-center gap-3 border-b border-charcoal-blue-200/70 p-4 dark:border-white/10">
								<div className="relative h-11 w-11 shrink-0 overflow-hidden rounded-2xl ring-1 ring-brand-500/20">
									<Image src={logoTransparent} alt="Mizan" fill sizes="44px" className="object-cover" />
								</div>
								<div>
									<p className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Mizan</p>
									<p className="text-[10px] uppercase tracking-[0.18em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
										ሚዛን • Balance
									</p>
								</div>
								<button
									type="button"
									onClick={() => setMobileSheetOpen(false)}
									className="ml-auto flex h-9 w-9 items-center justify-center rounded-xl text-charcoal-blue-500 hover:text-charcoal-blue-900 dark:text-charcoal-blue-300 dark:hover:text-white"
									aria-label="Close menu"
								>
									<AnimatedIcon name="x" size={16} />
								</button>
							</div>

							<div className="custom-scrollbar min-h-0 flex-1 overflow-y-auto px-3 py-4">
								<p className="px-2 pb-2 text-[10px] font-semibold uppercase tracking-[0.18em] text-charcoal-blue-400">
									Main
								</p>
								<nav className="space-y-1" onClick={() => setMobileSheetOpen(false)}>
									{visiblePrimary.map((item) => (
										<SidebarLink key={item.href} item={item} collapsed={false} />
									))}
								</nav>
								<div className="my-3 border-t border-charcoal-blue-200/60 dark:border-white/5" />
								<p className="px-2 pb-2 text-[10px] font-semibold uppercase tracking-[0.18em] text-charcoal-blue-400">
									Account
								</p>
								<nav className="space-y-1" onClick={() => setMobileSheetOpen(false)}>
									{visibleSecondary.map((item) => (
										<SidebarLink key={item.href} item={item} collapsed={false} />
									))}
								</nav>
							</div>

							<div className="shrink-0 border-t border-charcoal-blue-200/70 p-4 dark:border-white/10">
								<button
									type="button"
									onClick={() => {
										setMobileSheetOpen(false);
										setShowLogoutModal(true);
									}}
									className="btn-ghost w-full justify-center text-red-600 dark:text-red-400"
								>
									<AnimatedIcon name="logout" size={16} />
									Sign out
								</button>
							</div>
						</aside>
					</>
				)}

				{/* Page content */}
				<main className="flex-1 px-4 pb-24 pt-6 sm:px-6 lg:px-8 lg:pb-10">
					<div className="page-transition mx-auto w-full max-w-7xl">{children}</div>
				</main>

				{/* Mobile bottom nav */}
				<nav
					aria-label="Primary"
					className="fixed inset-x-0 bottom-0 z-30 flex items-stretch gap-1 border-t border-charcoal-blue-200 bg-white/95 px-2 pb-[calc(env(safe-area-inset-bottom,0)+0.25rem)] pt-1.5 backdrop-blur-xl dark:border-white/10 dark:bg-charcoal-blue-950/95 lg:hidden"
				>
					{BOTTOM_NAV.map((item) => (
						<BottomNavLink key={item.href} item={item} />
					))}
				</nav>
			</div>

			{/* Logout modal */}
			{showLogoutModal &&
				typeof document !== "undefined" &&
				createPortal(
					<div
						className="fixed inset-0 z-[100] flex items-center justify-center bg-charcoal-blue-950/40 p-4 backdrop-blur-sm"
						onClick={() => setShowLogoutModal(false)}
					>
						<div
							className="surface-panel w-full max-w-sm p-6"
							onClick={(event) => event.stopPropagation()}
						>
							<div className="mb-5 flex items-start gap-4">
								<span className="icon-chip h-12 w-12 text-red-500 dark:text-red-400">
									<AnimatedIcon name="logout" size={20} />
								</span>
								<div className="space-y-1">
									<h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">Sign out</h3>
									<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
										Your session will end on this device immediately.
									</p>
								</div>
							</div>
							<div className="flex gap-3">
								<button
									type="button"
									onClick={() => setShowLogoutModal(false)}
									className="btn-secondary flex-1"
								>
									Cancel
								</button>
								<button
									type="button"
									onClick={() => {
										setShowLogoutModal(false);
										handleLogout();
									}}
									className="btn-danger flex-1"
								>
									Sign out
								</button>
							</div>
						</div>
					</div>,
					document.body
				)}
		</div>
	);
}
