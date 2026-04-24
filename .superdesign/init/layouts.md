# Layout Components

Shared layout shell and navigation components. The app is single-shell: every route wraps in the root `app/layout.tsx` (Navbar + main + Footer). Route groups `(auth)`, `(dashboard)`, `admin` add only auth guards / suspense, not visual chrome.

---

## Root Layout

**Path:** `frontend/app/layout.tsx`
**Description:** Server component that owns the `<html>`/`<body>`, mounts the global Toaster + AppearanceSync, renders `<Navbar/>` sticky at the top, a max-w-7xl `<main>` container with page-transition animation, and a branded `<footer>` with logo, copyright, and social icon chips. Applies appearance classes (light/dark/compact/reduce-motion) from the user session.

```tsx
import type { Metadata } from "next";
import Image from "next/image";
import "./globals.css";
import Navbar from "@/components/Navbar";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { Toaster } from "@/components/ui/sonner";
import { AppearanceSync } from "@/components/appearance/AppearanceSync";
import { getUserOptionalServer } from "@/helper/session";
import { getAppearanceSettingsFromUser, getServerAppearanceClasses } from "@/lib/appearance";
import logoTransparent from "@/public/logo_transparent.png";
import 'remixicon/fonts/remixicon.css';

export const dynamic = 'force-dynamic';

export const metadata: Metadata = {
	title: "Mizan - Balanced Nutrition & Fitness",
	description: "Your personal nutrition and fitness companion. Track meals, plan diets, and achieve your health goals with AI-powered coaching.",
};

export default function RootLayout(
	{ children }: Readonly<{ children: React.ReactNode }>
) {
	const userPromise = getUserOptionalServer();

	return <LayoutContent userPromise={userPromise}>{children}</LayoutContent>;
}

async function LayoutContent({
	children,
	userPromise,
}: Readonly<{ children: React.ReactNode; userPromise: ReturnType<typeof getUserOptionalServer> }>) {
	const user = await userPromise;
	const htmlClasses = getServerAppearanceClasses(getAppearanceSettingsFromUser(user));

	return (
		<html lang="en" className={htmlClasses.join(" ")}>
			<body className="min-h-screen antialiased flex flex-col selection:bg-brand-500/15 selection:text-slate-950 dark:selection:text-white">
				<AppearanceSync />
				<Toaster position="top-right" />
				<Navbar />
				<main className="grow pb-8">
					<div className="page-transition max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6 sm:py-8 lg:py-10">
						{children}
					</div>
				</main>

				<footer className="px-4 pb-4 sm:px-6 lg:px-8 lg:pb-6">
					<div className="surface-panel max-w-7xl mx-auto px-5 py-6 sm:px-6 sm:py-7">
						<div className="grid grid-cols-1 gap-6 text-center sm:grid-cols-3 sm:text-left">
							<div className="flex items-center justify-center gap-3 sm:justify-start">
								<div className="relative h-11 w-11 overflow-hidden rounded-2xl ring-1 ring-brand-500/20">
									<Image src={logoTransparent} alt="Mizan" fill className="object-cover" priority />
								</div>
								<div>
									<p className="text-lg font-semibold text-slate-950 dark:text-slate-50">
										Mizan
									</p>
									<p className="text-sm text-slate-500 dark:text-slate-400">ሚዛን • Balanced nutrition, training, and coaching.</p>
								</div>
							</div>

							<div className="flex flex-col items-center justify-center gap-2 sm:flex-row sm:gap-4">
								<p className="text-sm text-slate-500 dark:text-slate-400">
									&copy; {new Date().getFullYear()} Mizan
								</p>
								<span className="hidden text-slate-300 dark:text-slate-600 sm:inline">|</span>
								<a href="/privacy" className="footer-link">
									Privacy
								</a>
								<span className="hidden text-slate-300 dark:text-slate-600 sm:inline">|</span>
								<a href="/terms" className="footer-link">
									Terms
								</a>
							</div>

							<div className="flex items-center justify-center gap-3 sm:justify-end">
								<a href="#" className="icon-chip h-11 w-11 text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white" aria-label="GitHub">
									<AnimatedIcon name="github" size={18} aria-hidden="true" />
								</a>
								<a href="#" className="icon-chip h-11 w-11 text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white" aria-label="Twitter">
									<AnimatedIcon name="twitter" size={18} aria-hidden="true" />
								</a>
							</div>
						</div>
					</div>
				</footer>
			</body>
		</html>
	);
}
```

---

## Navbar (server shell)

**Path:** `frontend/components/Navbar/index.tsx`
**Description:** Sticky top glass-pill navbar (max-w-7xl, rounded-[30px], `nav-glass` class). Server-fetches the current user and renders the `<Image>` logo + wordmark, passing the user to `<NavbarContent>` which handles interactivity.

```tsx
import Link from "next/link";
import logoTransparent from "@/public/logo_transparent.png";
import Image from "next/image";
import { getUserOptionalServer } from "@/helper/session";
import NavbarContent from "./NavbarContent";

export default async function Navbar() {
	const user = await getUserOptionalServer();

	return (
		<nav className="sticky top-0 z-50 px-3 pt-3 sm:px-4 lg:px-6">
			<div className="max-w-7xl mx-auto">
				<div className="nav-glass relative flex h-16 items-center justify-between rounded-[30px] px-4 sm:px-6">
					<Link href="/" className="group flex items-center gap-3">
						<div className="relative">
							<div className="absolute inset-0 rounded-2xl bg-brand-500/10 blur-lg opacity-25 transition-opacity group-hover:opacity-40" />
							<Image
								src={logoTransparent}
								alt="Mizan"
								width={42}
								height={42}
								className="relative rounded-2xl"
							/>
						</div>
					<div className="flex flex-col">
						<span className="text-xl font-semibold text-slate-950 dark:text-slate-50">
							Mizan
						</span>
						<span className="hidden items-center gap-1 text-[10px] uppercase tracking-[0.18em] text-slate-500 dark:text-slate-400 sm:inline-flex">
							ሚዛን • Balance
						</span>
					</div>
					</Link>
					<NavbarContent user={user} />
				</div>
			</div>
		</nav>
	);
}
```

---

## NavbarContent (client interactive)

**Path:** `frontend/components/Navbar/NavbarContent.tsx`
**Description:** Client component handling primary nav links (Foods, Recipes, Meals, Admin), user avatar menu (Overview/Settings/MCP/Sessions/Sign out), mobile hamburger sheet, logout confirmation modal, and active-route detection via `usePathname`.

```tsx
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
					sizes="36px"
					className="object-cover"
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

	const roleLabel = user?.role && user.role !== "user" ? user.role : null;

	useEffect(() => {
		document.body.style.overflow = menuOpen ? 'hidden' : '';
		return () => { document.body.style.overflow = ''; };
	}, [menuOpen]);

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
		return () => document.removeEventListener("mousedown", handleClickOutside);
	}, [menuOpen, userMenuOpen]);

	useEffect(() => {
		if (!menuOpen && !userMenuOpen) return;
		const handleEscape = (event: KeyboardEvent) => {
			if (event.key !== "Escape") return;
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

	function closeMenu() { setMenuOpen(false); }
	function closeAllMenus() { setMenuOpen(false); setUserMenuOpen(false); }
	function toggleUserMenu() { setMenuOpen(false); setUserMenuOpen((open) => !open); }
	function toggleMobileMenu() { setUserMenuOpen(false); setMenuOpen((open) => !open); }

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
								className="group flex items-center gap-2 rounded-full border border-slate-200 bg-white px-2 py-1.5 text-sm text-slate-700 shadow-sm transition-colors hover:border-slate-300 hover:bg-white dark:border-white/10 dark:bg-slate-950 dark:text-slate-200 dark:hover:border-white/15 dark:hover:bg-slate-950"
							>
								<UserAvatar user={user} />
								<div className="hidden text-left sm:block">
									<p className="max-w-28 truncate font-medium text-slate-950 dark:text-white">{user.name || user.email}</p>
									{roleLabel ? <p className="max-w-28 truncate text-[11px] text-slate-500 dark:text-slate-400">{roleLabel}</p> : null}
								</div>
								<ChevronDown className={cn("h-4 w-4 text-slate-400 transition-transform duration-200 dark:text-slate-500", userMenuOpen && "rotate-180")} aria-hidden="true" />
							</button>

							{userMenuOpen && (
								<div ref={userMenuRef} data-testid="nav-user-menu" className="absolute right-0 top-[calc(100%+0.5rem)] z-70 mt-0 w-60 overflow-hidden rounded-[28px] border border-slate-200 bg-white p-1.5 shadow-2xl shadow-slate-950/10 animate-fade-in dark:border-white/10 dark:bg-slate-950">
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
					className="inline-flex items-center justify-center rounded-2xl border border-white/70 bg-white/80 dark:border-white/10 dark:bg-slate-950/75 h-11 w-11 text-slate-600 hover:text-slate-950 dark:text-slate-300 dark:hover:text-white md:hidden relative z-100"
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
						<div className="fixed inset-0 z-80 bg-slate-950/30 backdrop-blur-[2px] md:hidden" onClick={closeMenu} />
						<div ref={menuRef} id="mobile-nav-menu" data-testid="nav-mobile-menu" className="fixed inset-x-3 top-24 z-90 overflow-hidden rounded-[28px] border border-slate-200 bg-white p-4 shadow-2xl shadow-slate-950/15 animate-fade-in md:hidden dark:border-white/10 dark:bg-slate-950">
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
									<div className="flex items-center justify-between gap-3 rounded-2xl border border-slate-200 bg-slate-50 p-3 dark:border-white/10 dark:bg-slate-900">
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
```

---

## Dashboard group guard layout

**Path:** `frontend/app/(dashboard)/layout.tsx`
**Description:** Non-visual wrapper that server-checks session and redirects unauthenticated users to `/login`. All (dashboard) routes use the root layout's Navbar + Footer.

```tsx
import { getUserOptionalServer } from "@/helper/session";
import { redirect } from "next/navigation";

export default async function DashboardLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    const user = await getUserOptionalServer();

    if (!user) {
        redirect("/login");
    }

    return <>{children}</>;
}
```

---

## Admin guard layout

**Path:** `frontend/app/admin/layout.tsx`
**Description:** Server-checks session + admin role; redirects non-admins. Like the dashboard layout, adds no visual chrome.

```tsx
import { getUserOptionalServer } from "@/helper/session";
import { redirect } from "next/navigation";

export default async function AdminLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    const user = await getUserOptionalServer();

    if (!user) {
        redirect("/login");
    }

    // Enforce admin role verification
    if (user.role !== "admin") {
        redirect("/");
    }

    return <>{children}</>;
}
```

---

## Auth, login Suspense layout

**Path:** `frontend/app/(auth)/login/layout.tsx`
**Description:** Only special-case auth layout, wraps the login page in `<Suspense fallback={<Loading />}>` so `useSearchParams` can be read client-side. All other auth pages (register, verify, forgot-password) use the root layout directly.

```tsx
import Loading from "@/components/Loading";
import { Suspense } from "react";


export default function Layout(
	{ children }: Readonly<{ children: React.ReactNode }>
) {
	return (
		<Suspense fallback={<Loading />}>
			{children}
		</Suspense>
	)
}
```

---

## Verify-email Suspense layout

**Path:** `frontend/app/(auth)/verifyemail/layout.tsx`
**Description:** Same pattern as login, wraps the verifyemail page in a `<Suspense>` fallback.

Layout structure is essentially the same as the login layout above. Confirm in-repo before copying.

---

## Notes on shell architecture

- **Single-shell design:** unlike many dashboard apps, Mizan has no sidebar. Primary navigation (Foods/Recipes/Meals/Admin) lives in the top Navbar; user-scoped nav (Overview/Settings/MCP/Sessions) is in the avatar dropdown.
- **No separate marketing shell:** the landing hero in `app/page.tsx` sits inside the same Navbar/Footer shell, auth state just toggles hero vs dashboard-snapshot.
- **Route-group layouts are guards only**, they do not paint UI. To redesign the app shell, edit `app/layout.tsx` and `components/Navbar/*` only.
