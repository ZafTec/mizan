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
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { cn } from "@/lib/utils";
import { useSubscription } from "@/lib/hooks/useSubscription";
import { ProBadge } from "@/components/billing/ProBadge";
import logoTransparent from "@/public/logo_transparent.png";
import { NotificationBell } from "@/components/NotificationBell";
import HouseholdSwitcher from "./HouseholdSwitcher";
import LogSheet from "./LogSheet";
import { SPINE, USER_MENU, isActive, type NavItem } from "./nav";

function UserAvatar({ user, size = 36, pro = false }: { user: User; size?: number; pro?: boolean }) {
	const ringClass = pro
		? "ring-2 ring-brand-500 shadow-md shadow-brand-500/30"
		: "ring-1 ring-brand-500/15";

	if (user.image) {
		return (
			<div
				className={cn("relative overflow-hidden rounded-2xl", ringClass)}
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
			className={cn("flex items-center justify-center rounded-2xl bg-brand-600 font-semibold text-white dark:bg-brand-500", ringClass)}
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
				"press-feedback group relative flex items-center gap-3 rounded-2xl px-3 py-2.5 text-sm font-medium transition-[background-color,color,box-shadow] duration-160 ease-out",
				active
					? "bg-brand-600 text-white shadow-lg shadow-brand-500/25 dark:bg-brand-500 dark:text-charcoal-blue-950"
					: "text-charcoal-blue-600 hover:bg-white/70 hover:text-charcoal-blue-900 dark:text-charcoal-blue-200 dark:hover:bg-white/5 dark:hover:text-charcoal-blue-50",
				collapsed && "justify-center px-2"
			)}
			title={collapsed ? item.label : undefined}
		>
			<span
				className={cn(
					"relative flex h-5 w-5 shrink-0 items-center justify-center",
					active
						? "text-white dark:text-charcoal-blue-950"
						: "text-charcoal-blue-500 group-hover:text-current dark:text-charcoal-blue-300"
				)}
			>
				<AnimatedIcon name={item.icon} size={18} aria-hidden="true" />
			</span>
			{!collapsed && <span className="truncate">{item.label}</span>}
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
	const { isPro } = useSubscription();
	const [collapsed, setCollapsed] = useState(false);
	const [userMenuOpen, setUserMenuOpen] = useState(false);
	const [userMenuPos, setUserMenuPos] = useState<{ top: number; right: number } | null>(null);
	const [logSheetOpen, setLogSheetOpen] = useState(false);
	const [showLogoutModal, setShowLogoutModal] = useState(false);
	const userMenuRef = useRef<HTMLDivElement>(null);
	const userTriggerRef = useRef<HTMLButtonElement>(null);

	// Anchor the portal'd user menu to the trigger's viewport rect. Refresh on
	// scroll/resize while open so the menu tracks the button.
	useEffect(() => {
		if (!userMenuOpen) return;
		const sync = () => {
			const el = userTriggerRef.current;
			if (!el) return;
			const rect = el.getBoundingClientRect();
			setUserMenuPos({ top: rect.bottom + 8, right: Math.max(8, window.innerWidth - rect.right) });
		};
		sync();
		window.addEventListener("scroll", sync, true);
		window.addEventListener("resize", sync);
		return () => {
			window.removeEventListener("scroll", sync, true);
			window.removeEventListener("resize", sync);
		};
	}, [userMenuOpen]);

	const isAdmin = user.role === "admin";
	const visibleSecondary = USER_MENU.filter((item) => !item.adminOnly || isAdmin);

	// Menus close via their own onClick handlers, not via a pathname-tracking effect.

	useEffect(() => {
		function onClick(event: MouseEvent) {
			const target = event.target as HTMLElement;
			if (userMenuOpen && userMenuRef.current && !userMenuRef.current.contains(target)) {
				if (!target.closest("[data-app-shell-user-trigger]")) {
					setUserMenuOpen(false);
				}
			}
		}
		function onEscape(event: KeyboardEvent) {
			if (event.key === "Escape") {
				setUserMenuOpen(false);
			}
		}
		document.addEventListener("mousedown", onClick);
		document.addEventListener("keydown", onEscape);
		return () => {
			document.removeEventListener("mousedown", onClick);
			document.removeEventListener("keydown", onEscape);
		};
	}, [userMenuOpen]);

	async function handleLogout() {
		try {
			clearAppearanceCookie();
			await signOut();
			router.push("/");
			router.refresh();
		} catch (error) {
			appToast.error(error, "Failed to sign out");
		}
	}

	const roleLabel = user.role && user.role !== "user" ? user.role : null;

	return (
		<div className="shell-fullbleed relative flex h-dvh overflow-x-clip bg-[color-mix(in_oklab,var(--color-charcoal-blue-50)_92%,white)] dark:bg-[color-mix(in_oklab,var(--color-charcoal-blue-950)_92%,black)]">
			{/* Soft decorative blobs (v2 aesthetic). Kept inside the shell so the body
				never scrolls to reveal them; html/body overflow-x: clip is the backstop. */}
			<div aria-hidden="true" className="pointer-events-none absolute right-[-5%] top-[-10%] h-125 w-125 rounded-full bg-verdigris-200/30 blur-[120px] -z-10" />
			<div aria-hidden="true" className="pointer-events-none absolute bottom-[-10%] left-[-5%] h-100 w-100 rounded-full bg-sandy-brown-200/25 blur-[100px] -z-10" />

			{/* Desktop Sidebar, fills shell height; inner nav scrolls via custom-scrollbar */}
			<aside
				className={cn(
					"hidden h-full shrink-0 flex-col border-r border-charcoal-blue-200 bg-white dark:border-white/10 dark:bg-charcoal-blue-950 lg:flex",
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

				<div className={cn("shrink-0 px-3 pt-4", collapsed && "px-2")}>
					<button
						type="button"
						onClick={() => setLogSheetOpen(true)}
						className={cn(
							"flex w-full items-center justify-center gap-2 rounded-2xl bg-brand-600 py-3 font-semibold text-white shadow-lg shadow-brand-600/25 transition-colors hover:bg-brand-700 dark:bg-brand-500 dark:hover:bg-brand-400",
							collapsed && "px-0"
						)}
						aria-label="Log an entry"
						title="Log an entry"
					>
						<span className="text-xl leading-none">+</span>
						{!collapsed && <span>Log</span>}
					</button>
				</div>

				<nav className={cn("custom-scrollbar flex-1 space-y-1 overflow-y-auto px-3 py-4", collapsed && "px-2")}>
					{SPINE.map((item) => (
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
						<div
							className={cn(
								"flex items-center gap-3 rounded-2xl border p-2.5",
								isPro
									? "border-brand-500/30 bg-gradient-to-r from-brand-500/10 to-transparent dark:border-brand-500/25"
									: "border-charcoal-blue-200 bg-white dark:border-white/10 dark:bg-charcoal-blue-900"
							)}
						>
							<UserAvatar user={user} size={36} pro={isPro} />
							<div className="min-w-0 flex-1 leading-tight">
								<div className="flex items-center gap-1.5">
									<p className="truncate text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
										{user.name || user.email}
									</p>
									{isPro && <ProBadge />}
								</div>
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

			{/* Main column, full shell height; only <main> scrolls internally */}
			<div className="flex h-full min-w-0 flex-1 flex-col">
				{/* Top bar, shrink-0 so it stays visible while main scrolls */}
				<header className="shrink-0 border-b border-charcoal-blue-200 bg-white dark:border-white/10 dark:bg-charcoal-blue-950">
					<div className="flex h-16 items-center gap-3 px-4 sm:px-6 lg:px-8">
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
								className="h-10 w-full rounded-xl border border-charcoal-blue-200 bg-white pl-10 pr-4 text-sm text-charcoal-blue-900 placeholder-charcoal-blue-400 outline-none transition-colors focus:border-verdigris-500 focus:ring-4 focus:ring-verdigris-300/20 dark:border-white/10 dark:bg-charcoal-blue-900 dark:text-charcoal-blue-50 dark:placeholder-charcoal-blue-400"
							/>
							<span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-charcoal-blue-400">
								<AnimatedIcon name="search" size={16} />
							</span>
						</div>

						<div className="ml-auto flex items-center gap-2">
							<HouseholdSwitcher />
							<NotificationBell />
							<div className="relative">
								<button
									ref={userTriggerRef}
									type="button"
									data-app-shell-user-trigger
									onClick={() => setUserMenuOpen((o) => !o)}
									className="flex items-center gap-2 rounded-full border border-charcoal-blue-200 bg-white px-2 py-1.5 text-sm text-charcoal-blue-700 transition-colors hover:border-charcoal-blue-300 dark:border-white/10 dark:bg-charcoal-blue-900 dark:text-charcoal-blue-200"
									aria-expanded={userMenuOpen}
									aria-haspopup="menu"
								>
									<UserAvatar user={user} size={30} pro={isPro} />
									<span className="hidden items-center gap-1.5 sm:flex">
										<span className="max-w-40 truncate font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
											{user.name || user.email?.split("@")[0]}
										</span>
										{isPro && <ProBadge />}
									</span>
									<ChevronDown
										className={cn(
											"h-4 w-4 text-charcoal-blue-400 transition-transform",
											userMenuOpen && "rotate-180"
										)}
									/>
								</button>
								{/* User menu is portalled to document.body so any overflow/isolate
								    ancestor (sticky header, scroll containers) can't clip it. */}
								{userMenuOpen && userMenuPos &&
									createPortal(
										<div
											ref={userMenuRef}
											role="menu"
											className="menu-pop fixed w-60 overflow-hidden rounded-xl border border-charcoal-blue-200 bg-white p-1.5 shadow-2xl shadow-charcoal-blue-950/15 dark:border-white/10 dark:bg-charcoal-blue-950"
											style={{ top: userMenuPos.top, right: userMenuPos.right, zIndex: 1000 }}
										>
											<div className="mb-1 rounded-2xl px-3 py-2.5">
												<div className="flex items-center gap-1.5">
													<p className="truncate text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
														{user.name || user.email}
													</p>
													{isPro && <ProBadge />}
												</div>
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
														{item.href === "/billing" && isPro && <ProBadge className="ml-auto" />}
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
										</div>,
										document.body,
									)}
							</div>
						</div>
					</div>
				</header>

				{/* Page content, flex-1 fills the column so long pages scroll inside
					it and short pages don't leave unclaimed space. Scrolls internally so
					the body stays viewport-sized. */}
				<main className="custom-scrollbar min-h-0 flex-1 overflow-y-auto px-4 pt-6 pb-[calc(4.5rem+env(safe-area-inset-bottom,0))] sm:px-6 lg:px-8 lg:pb-10">
					<div className="page-transition mx-auto w-full max-w-7xl">{children}</div>
				</main>

				{/* Mobile bottom nav */}
				<nav
					aria-label="Primary"
					className="fixed inset-x-0 bottom-0 z-30 flex items-stretch gap-1 border-t border-charcoal-blue-200 bg-white px-2 pb-[calc(env(safe-area-inset-bottom,0)+0.25rem)] pt-1.5 dark:border-white/10 dark:bg-charcoal-blue-950 lg:hidden"
				>
					{SPINE.slice(0, 2).map((item) => (
						<BottomNavLink key={item.href} item={item} />
					))}
					<button
						type="button"
						onClick={() => setLogSheetOpen(true)}
						className="flex flex-1 flex-col items-center justify-center gap-0.5 py-1"
						aria-label="Log an entry"
					>
						<span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-brand-600 text-2xl leading-none text-white shadow-lg shadow-brand-600/30 dark:bg-brand-500">
							+
						</span>
					</button>
					{SPINE.slice(2).map((item) => (
						<BottomNavLink key={item.href} item={item} />
					))}
				</nav>
			</div>

			<LogSheet open={logSheetOpen} onClose={() => setLogSheetOpen(false)} />

			{/* Logout modal */}
			{showLogoutModal &&
				typeof document !== "undefined" &&
				createPortal(
					<div
						className="modal-overlay-in fixed inset-0 z-100 flex items-center justify-center bg-charcoal-blue-950/40 p-4 backdrop-blur-sm"
						onClick={() => setShowLogoutModal(false)}
					>
						<div
							className="modal-pop-in surface-panel w-full max-w-sm p-6"
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
