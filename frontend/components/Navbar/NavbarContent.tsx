'use client';

import type { User } from "@/lib/auth";
import Link from "next/link";
import Image from "next/image";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { ChevronDown } from "lucide-react";
import { signOut } from "@/lib/auth-client";
import { appToast } from "@/lib/toast";
import { AnimatedIcon, type AnimatedIconName } from "@/components/ui/animated-icon";
import { cn } from "@/lib/utils";

interface NavbarContentProps {
	user: User | null;
}

interface NavItem {
	href: string;
	label: string;
	icon: AnimatedIconName;
}

function UserAvatar({ user }: { user: User }) {
	if (user.image) {
		return (
			<div className="relative h-9 w-9 overflow-hidden rounded-2xl ring-1 ring-brand-500/15">
				<Image
					src={user.image}
					alt={user.name || user.email || 'User'}
					fill
					className="object-cover"
					unoptimized
				/>
			</div>
		);
	}

	return (
		<div className="flex h-9 w-9 items-center justify-center rounded-2xl bg-brand-600 text-sm font-semibold text-white ring-1 ring-brand-500/15 dark:bg-brand-500">
			{user.email?.charAt(0).toUpperCase() || 'U'}
		</div>
	);
}

function NavLink({
	item,
	onClick,
	mobile = false,
}: {
	item: NavItem;
	onClick?: () => void;
	mobile?: boolean;
}) {
	const pathname = usePathname();
	const isActive = pathname === item.href || pathname?.startsWith(`${item.href}/`);

	return (
		<Link
			href={item.href}
			onClick={onClick}
			className={cn(
				"group flex items-center gap-3 px-3 py-2 text-sm font-medium transition-colors duration-200",
				isActive
					? "text-slate-950 dark:text-white"
					: "text-slate-600 hover:text-slate-950 dark:text-slate-300 dark:hover:text-white",
				mobile ? "w-full rounded-2xl hover:bg-slate-900/5 dark:hover:bg-white/5" : "rounded-full"
			)}
		>
			<span
				className={cn(
					mobile
						? "icon-chip h-9 w-9 text-slate-500 dark:text-slate-300"
						: "flex h-5 w-5 items-center justify-center text-slate-400 dark:text-slate-500",
					isActive ? "text-brand-600 dark:text-brand-300" : "group-hover:text-slate-950 dark:group-hover:text-white"
				)}
			>
				<AnimatedIcon name={item.icon} size={16} aria-hidden="true" />
			</span>
			<span>{item.label}</span>
		</Link>
	);
}

export default function NavbarContent({ user }: NavbarContentProps) {
	const [menuOpen, setMenuOpen] = useState(false);
	const [showLogoutModal, setShowLogoutModal] = useState(false);
	const [userMenuOpen, setUserMenuOpen] = useState(false);
	const menuRef = useRef<HTMLDivElement>(null);
	const userMenuRef = useRef<HTMLDivElement>(null);
	const pathname = usePathname();
	const router = useRouter();

	const primaryItems: NavItem[] = [
		{ href: "/ingredients", label: "Foods", icon: "search" },
		{ href: "/recipes", label: "Recipes", icon: "cookingPot" },
		...(user ? [{ href: "/meals", label: "Meals", icon: "flame" as const }] : []),
		...(user?.role === 'admin' ? [{ href: "/admin", label: "Admin", icon: "shieldCheck" as const }] : []),
	];

	const accountItems: NavItem[] = user
		? [
				{ href: "/profile", label: "Overview", icon: "home" },
				{ href: "/profile/settings", label: "Settings", icon: "user" },
				{ href: "/profile/mcp", label: "MCP", icon: "bot" },
				{ href: "/profile/sessions", label: "Sessions", icon: "lock" },
		  ]
		: [];

	useEffect(() => {
		document.body.style.overflow = menuOpen ? 'hidden' : '';

		return () => {
			document.body.style.overflow = '';
		};
	}, [menuOpen]);

	useEffect(() => {
		closeAllMenus();
	}, [pathname]);

	useEffect(() => {
		const handleClickOutside = (event: MouseEvent) => {
			if (
				menuRef.current &&
				!menuRef.current.contains(event.target as Node) &&
				!(event.target as HTMLElement).closest("button[data-testid='nav-mobile-toggle']")
			) {
				setMenuOpen(false);
			}

			if (
				userMenuRef.current &&
				!userMenuRef.current.contains(event.target as Node) &&
				!(event.target as HTMLElement).closest("button[data-testid='nav-user-trigger']")
			) {
				setUserMenuOpen(false);
			}
		};

		if (menuOpen || userMenuOpen) {
			document.addEventListener("mousedown", handleClickOutside);
		}

		return () => {
			document.removeEventListener("mousedown", handleClickOutside);
		};
	}, [menuOpen, userMenuOpen]);

	useEffect(() => {
		if (!menuOpen && !userMenuOpen) {
			return;
		}

		const handleEscape = (event: KeyboardEvent) => {
			if (event.key !== "Escape") {
				return;
			}

			setMenuOpen(false);
			setUserMenuOpen(false);
		};

		window.addEventListener("keydown", handleEscape);
		return () => window.removeEventListener("keydown", handleEscape);
	}, [menuOpen, userMenuOpen]);

	async function handleLogout() {
		try {
			await signOut({
				fetchOptions: {
					onSuccess: () => {
						router.push("/");
						router.refresh();
					},
				},
		});
		} catch (error) {
			console.error("Logout failed:", error);
			appToast.error(error, "Failed to sign out");
		}
	}

	function closeMenu() {
		setMenuOpen(false);
	}

	function closeAllMenus() {
		setMenuOpen(false);
		setUserMenuOpen(false);
	}

	function toggleUserMenu() {
		setMenuOpen(false);
		setUserMenuOpen((open) => !open);
	}

	function toggleMobileMenu() {
		setUserMenuOpen(false);
		setMenuOpen((open) => !open);
	}

	return (
		<>
			{showLogoutModal &&
				createPortal(
					<div className="fixed inset-0 z-100 flex items-center justify-center bg-slate-950/35 p-4 backdrop-blur-sm" onClick={() => setShowLogoutModal(false)}>
						<div className="surface-panel w-full max-w-sm p-6" onClick={(event) => event.stopPropagation()}>
							<div className="mb-5 flex items-start gap-4">
								<span className="icon-chip h-12 w-12 text-red-500 dark:text-red-400">
									<AnimatedIcon name="logout" size={20} aria-hidden="true" />
								</span>
								<div className="space-y-1">
									<h3 className="text-lg font-semibold text-slate-950 dark:text-slate-50">Sign out</h3>
									<p className="text-sm text-slate-500 dark:text-slate-400">Your session will end on this device immediately.</p>
								</div>
							</div>
							<div className="flex gap-3">
								<button onClick={() => setShowLogoutModal(false)} className="btn-secondary flex-1">
									Cancel
								</button>
								<button
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

			<div className="flex items-center gap-2" data-testid="navbar">
				<nav className="hidden items-center gap-1 md:flex">
					{primaryItems.map((item) => (
						<NavLink key={item.href} item={item} />
					))}
				</nav>

				<div className="hidden items-center gap-2 md:flex">
					{user ? (
						<div className="relative z-60">
							<button
								type="button"
								data-testid="nav-user-trigger"
								onClick={toggleUserMenu}
								aria-expanded={userMenuOpen}
								aria-haspopup="menu"
								className="group flex items-center gap-2 rounded-full border border-slate-200/80 bg-white/90 px-2 py-1.5 text-sm text-slate-700 shadow-sm transition-colors hover:border-slate-300 hover:bg-white dark:border-white/10 dark:bg-slate-950/70 dark:text-slate-200 dark:hover:border-white/15 dark:hover:bg-slate-950"
							>
								<UserAvatar user={user} />
								<div className="hidden text-left sm:block">
									<p className="max-w-28 truncate font-medium text-slate-950 dark:text-white">{user.name || user.email}</p>
									<p className="max-w-28 truncate text-[11px] text-slate-500 dark:text-slate-400">{user.role || 'user'}</p>
								</div>
								<ChevronDown className={cn("h-4 w-4 text-slate-400 transition-transform duration-200 dark:text-slate-500", userMenuOpen && "rotate-180")} aria-hidden="true" />
							</button>

							{userMenuOpen && (
								<div ref={userMenuRef} data-testid="nav-user-menu" className="surface-panel absolute right-0 top-[calc(100%+0.5rem)] z-70 mt-0 w-60 p-1.5 animate-fade-in shadow-2xl shadow-slate-950/10">
									<div className="mb-1 rounded-2xl px-3 py-2.5">
										<p className="truncate text-sm font-semibold text-slate-950 dark:text-white">{user.name || user.email}</p>
										<p className="truncate text-xs text-slate-500 dark:text-slate-400">{user.email}</p>
									</div>
									<div className="space-y-1">
										{accountItems.map((item) => (
											<NavLink key={item.href} item={item} onClick={closeAllMenus} mobile />
										))}
									</div>
									<div className="mt-1 border-t border-slate-200/70 pt-1.5 dark:border-white/10">
										<button
											onClick={() => {
												closeAllMenus();
												setShowLogoutModal(true);
											}}
											className="group flex w-full items-center gap-3 rounded-2xl px-3 py-2 text-sm font-medium text-red-600 transition-colors hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-500/10"
										>
											<span className="flex h-5 w-5 items-center justify-center text-red-500 dark:text-red-400">
												<AnimatedIcon name="logout" size={16} aria-hidden="true" />
											</span>
											<span>Sign out</span>
										</button>
									</div>
								</div>
							)}
						</div>
					) : (
						<>
							<Link href="/login" className="px-4 py-2 text-sm font-medium text-slate-600 transition-colors hover:text-slate-950 dark:text-slate-300 dark:hover:text-white">
								Sign In
							</Link>
							<Link href="/register" className="btn-primary">
								Get Started
								<AnimatedIcon name="arrowRight" size={16} aria-hidden="true" />
							</Link>
						</>
					)}
				</div>

				<button
									type="button"
									className="icon-chip h-11 w-11 text-slate-600 hover:text-slate-950 dark:text-slate-300 dark:hover:text-white md:hidden"
									onClick={toggleMobileMenu}
									aria-label="Toggle navigation menu"
					aria-expanded={menuOpen}
					aria-controls="mobile-nav-menu"
					data-testid="nav-mobile-toggle"
				>
					<AnimatedIcon name={menuOpen ? "x" : "menu"} size={18} aria-hidden="true" />
				</button>

				{menuOpen && (
					<>
						<div className="fixed inset-0 z-40 bg-slate-950/25 backdrop-blur-[2px] md:hidden" onClick={closeMenu} />
						<div ref={menuRef} id="mobile-nav-menu" data-testid="nav-mobile-menu" className="surface-panel fixed inset-x-3 top-24 z-50 p-4 md:hidden animate-fade-in">
							<nav className="space-y-1">
								{primaryItems.map((item) => (
									<NavLink key={item.href} item={item} onClick={closeAllMenus} mobile />
								))}
								{accountItems.map((item) => (
									<NavLink key={item.href} item={item} onClick={closeAllMenus} mobile />
								))}
							</nav>

							<div className="mt-4 border-t border-slate-200/70 pt-4 dark:border-white/10">
								{user ? (
									<div className="flex items-center justify-between gap-3 rounded-2xl border border-white/55 bg-white/50 p-3 dark:border-white/10 dark:bg-white/5">
										<div className="flex items-center gap-3">
											<UserAvatar user={user} />
											<div>
												<p className="text-sm font-semibold text-slate-950 dark:text-white">{user.name || user.email}</p>
												<p className="text-xs text-slate-500 dark:text-slate-400">Signed in</p>
											</div>
										</div>
										<button
											onClick={() => {
												closeAllMenus();
												setShowLogoutModal(true);
											}}
											className="btn-ghost text-red-600 dark:text-red-400"
										>
											<AnimatedIcon name="logout" size={16} aria-hidden="true" />
											Sign out
										</button>
									</div>
								) : (
									<div className="flex gap-2">
										<Link href="/login" onClick={closeAllMenus} className="btn-secondary flex-1 justify-center">
											Sign In
										</Link>
										<Link href="/register" onClick={closeAllMenus} className="btn-primary flex-1 justify-center">
											Get Started
											<AnimatedIcon name="arrowRight" size={16} aria-hidden="true" />
										</Link>
									</div>
								)}
							</div>
						</div>
					</>
				)}
			</div>
		</>
	);
}
