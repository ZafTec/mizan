'use client';

import type { User } from "@/lib/auth";
import Link from "next/link";
import Image from "next/image";
import { useRouter, usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { signOut } from "@/lib/auth-client";

interface NavbarContentProps {
	user: User | null;
}

const NavLink = ({ href, children, onClick }: { href: string; children: React.ReactNode; onClick?: () => void }) => {
	const pathname = usePathname();
	const isActive = pathname === href || pathname?.startsWith(href + '/');

	return (
		<Link
			href={href}
			onClick={onClick}
			className={`relative px-3 py-2 text-sm font-medium transition-colors rounded-lg ${
				isActive
					? 'text-brand-600 bg-brand-50 dark:text-brand-400 dark:bg-brand-950'
					: 'text-slate-600 hover:text-slate-900 hover:bg-slate-100 dark:text-slate-400 dark:hover:text-slate-100 dark:hover:bg-slate-800'
			}`}
		>
			{children}
		</Link>
	);
};

export default function NavbarContent({ user }: NavbarContentProps) {
	const [menuOpen, setMenuOpen] = useState(false);
	const [showLogoutModal, setShowLogoutModal] = useState(false);
	const [userMenuOpen, setUserMenuOpen] = useState(false);
	const menuRef = useRef<HTMLDivElement>(null);
	const userMenuRef = useRef<HTMLDivElement>(null);
	const router = useRouter();

	useEffect(() => {
		if (menuOpen) {
			document.body.style.overflow = 'hidden';
		} else {
			document.body.style.overflow = '';
		}
		return () => {
			document.body.style.overflow = '';
		};
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

		return () => {
			document.removeEventListener("mousedown", handleClickOutside);
		};
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
		}
	}

	const closeMenu = () => setMenuOpen(false);

	return (
		<>
		{showLogoutModal && (
			<div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4" onClick={() => setShowLogoutModal(false)}>
				<div className="bg-white dark:bg-slate-900 rounded-2xl p-6 max-w-sm w-full shadow-xl" onClick={(e) => e.stopPropagation()}>
					<div className="flex items-center gap-3 mb-4">
						<div className="w-10 h-10 rounded-xl bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
							<i className="ri-logout-box-r-line text-xl text-red-600 dark:text-red-400" />
						</div>
						<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Sign Out</h3>
					</div>
					<p className="text-sm text-slate-500 dark:text-slate-400 mb-6">Are you sure you want to sign out?</p>
					<div className="flex gap-3">
						<button
							onClick={() => setShowLogoutModal(false)}
							className="flex-1 btn-secondary"
						>
							Cancel
						</button>
						<button
							onClick={() => { setShowLogoutModal(false); handleLogout(); }}
							className="flex-1 btn-danger"
						>
							Sign Out
						</button>
					</div>
				</div>
			</div>
		)}
		<div className="flex items-center gap-2" data-testid="navbar">
			{/* Desktop Navigation */}
			<nav className="hidden md:flex items-center gap-1">
				<NavLink href="/ingredients">
					<i className="ri-leaf-line mr-1.5" />
					Foods
				</NavLink>
				<NavLink href="/recipes">
					<i className="ri-restaurant-line mr-1.5" />
					Recipes
				</NavLink>
				{user && (
					<>
						<NavLink href="/meals">
							<i className="ri-bowl-line mr-1.5" />
							Meals
						</NavLink>
						<NavLink href="/meal-plan">
							<i className="ri-calendar-schedule-line mr-1.5" />
							Meal Plan
						</NavLink>
					</>
				)}
				{user?.role === 'admin' && (
					<NavLink href="/admin">
						<i className="ri-shield-user-line mr-1.5" />
						Admin
					</NavLink>
				)}
			</nav>

			{/* User Actions */}
			<div className="hidden md:flex items-center gap-2 ml-4 pl-4 border-l border-slate-200 dark:border-slate-700">
				{user ? (
					<>
						<div className="relative">
							<button
								data-testid="nav-user-trigger"
								onClick={() => setUserMenuOpen(!userMenuOpen)}
								className="flex items-center gap-2 px-3 py-2 text-sm text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
							>
								{user.image ? (
									<div className="relative w-8 h-8 rounded-full overflow-hidden ring-2 ring-brand-500/20">
										<Image
											src={user.image}
											alt={user.name || user.email || 'User'}
											fill
											className="object-cover"
											unoptimized
										/>
									</div>
								) : (
									<div className="w-8 h-8 rounded-full bg-linear-to-br from-brand-400 to-brand-600 flex items-center justify-center text-white text-sm font-medium ring-2 ring-brand-500/20">
										{user.email?.charAt(0).toUpperCase() || 'U'}
									</div>
								)}
								<i className={`ri-arrow-down-s-line transition-transform ${userMenuOpen ? 'rotate-180' : ''}`} />
							</button>

							{userMenuOpen && (
								<div
									ref={userMenuRef}
									data-testid="nav-user-menu"
									className="absolute right-0 top-full mt-2 w-56 bg-white dark:bg-slate-900 rounded-2xl shadow-xl border border-slate-200 dark:border-slate-700 p-2 animate-fade-in z-50"
								>
									<div className="px-3 py-2 mb-1 border-b border-slate-100 dark:border-slate-800">
										<p className="text-sm font-medium text-slate-900 dark:text-slate-100 truncate">{user.name || user.email}</p>
										<p className="text-xs text-slate-500 dark:text-slate-400 truncate">{user.email}</p>
									</div>
									<Link href="/profile" onClick={() => setUserMenuOpen(false)} className="flex items-center gap-2 px-3 py-2 text-sm text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg">
										<i className="ri-user-line" /> Profile
									</Link>
									<Link href="/goal" onClick={() => setUserMenuOpen(false)} className="flex items-center gap-2 px-3 py-2 text-sm text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg">
										<i className="ri-target-line" /> Goals
									</Link>
									<Link href="/workouts" onClick={() => setUserMenuOpen(false)} className="flex items-center gap-2 px-3 py-2 text-sm text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg">
										<i className="ri-boxing-line" /> Workouts
									</Link>
									<Link href="/achievements" onClick={() => setUserMenuOpen(false)} className="flex items-center gap-2 px-3 py-2 text-sm text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg">
										<i className="ri-trophy-line" /> Achievements
									</Link>
									<Link href="/body-measurements" onClick={() => setUserMenuOpen(false)} className="flex items-center gap-2 px-3 py-2 text-sm text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg">
										<i className="ri-scales-3-line" /> Body Measurements
									</Link>
									<div className="mt-1 pt-1 border-t border-slate-100 dark:border-slate-800">
										<button
											onClick={() => { setUserMenuOpen(false); setShowLogoutModal(true); }}
											className="flex items-center gap-2 w-full px-3 py-2 text-sm text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950 rounded-lg"
										>
											<i className="ri-logout-box-r-line" /> Sign Out
										</button>
									</div>
								</div>
							)}
						</div>
					</>
				) : (
					<>
						<Link
							href="/login"
							className="px-4 py-2 text-sm font-medium text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100 transition-colors"
						>
							Sign In
						</Link>
						<Link
							href="/register"
							className="px-4 py-2 text-sm font-medium text-white bg-linear-to-r from-brand-500 to-brand-600 rounded-xl hover:from-brand-600 hover:to-brand-700 shadow-lg shadow-brand-500/25 dark:shadow-brand-500/10 transition-all"
						>
							Get Started
						</Link>
					</>
				)}
			</div>

			{/* Mobile Menu Button */}
			<button
				className="md:hidden p-2 text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
				onClick={() => setMenuOpen(!menuOpen)}
				aria-label="Toggle navigation menu"
				aria-expanded={menuOpen}
				aria-controls="mobile-nav-menu"
				data-testid="nav-mobile-toggle"
			>
				<svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
					{menuOpen ? (
						<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
					) : (
						<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
					)}
				</svg>
			</button>

			{/* Mobile Menu Backdrop + Panel */}
			{menuOpen && (
				<>
					<div
						className="fixed inset-0 bg-black/30 z-40 md:hidden"
						onClick={closeMenu}
					/>
					<div
						ref={menuRef}
						id="mobile-nav-menu"
						data-testid="nav-mobile-menu"
						className="fixed top-[4.5rem] left-0 right-0 mx-4 p-4 bg-white dark:bg-slate-900 rounded-2xl shadow-xl border border-slate-200 dark:border-slate-700 md:hidden animate-fade-in z-50"
					>
						<nav className="flex flex-col gap-1">
							<NavLink href="/ingredients" onClick={closeMenu}>
								<i className="ri-leaf-line mr-2" />
								Foods
							</NavLink>
							<NavLink href="/recipes" onClick={closeMenu}>
								<i className="ri-restaurant-line mr-2" />
								Recipes
							</NavLink>
							{user && (
								<>
									<NavLink href="/meals" onClick={closeMenu}>
										<i className="ri-bowl-line mr-2" />
										Meals
									</NavLink>
									<NavLink href="/meal-plan" onClick={closeMenu}>
										<i className="ri-calendar-schedule-line mr-2" />
										Meal Plan
									</NavLink>
									<NavLink href="/goal" onClick={closeMenu}>
										<i className="ri-target-line mr-2" />
										Goals
									</NavLink>
									<NavLink href="/workouts" onClick={closeMenu}>
										<i className="ri-boxing-line mr-2" />
										Workouts
									</NavLink>
									<NavLink href="/trainers" onClick={closeMenu}>
										<i className="ri-user-heart-line mr-2" />
										Trainers
									</NavLink>
								</>
							)}
							{user?.role === 'admin' && (
								<NavLink href="/admin" onClick={closeMenu}>
									<i className="ri-shield-user-line mr-2" />
									Admin
								</NavLink>
							)}
						</nav>

						<div className="mt-4 pt-4 border-t border-slate-200 dark:border-slate-700">
							{user ? (
								<div className="flex items-center justify-between">
									<Link
										href="/profile"
										onClick={closeMenu}
										className="flex items-center gap-3 px-3 py-2 text-sm text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg"
									>
										{user.image ? (
											<div className="relative w-8 h-8 rounded-full overflow-hidden ring-2 ring-brand-500/20">
												<Image
													src={user.image}
													alt={user.name || user.email || 'User'}
													fill
													className="object-cover"
													unoptimized
												/>
											</div>
										) : (
											<div className="w-8 h-8 rounded-full bg-linear-to-br from-brand-400 to-brand-600 flex items-center justify-center text-white text-sm font-medium ring-2 ring-brand-500/20">
												{user.email?.charAt(0).toUpperCase() || 'U'}
											</div>
										)}
										<span>Profile</span>
									</Link>
									<button
										onClick={() => { closeMenu(); setShowLogoutModal(true); }}
										className="px-4 py-2 text-sm text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950 rounded-lg transition-colors"
									>
										Sign Out
									</button>
								</div>
							) : (
								<div className="flex gap-2">
									<Link
										href="/login"
										onClick={closeMenu}
										className="flex-1 px-4 py-2.5 text-sm font-medium text-center text-slate-600 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 rounded-xl hover:bg-slate-200 dark:hover:bg-slate-700 transition-colors"
									>
										Sign In
									</Link>
									<Link
										href="/register"
										onClick={closeMenu}
										className="flex-1 px-4 py-2.5 text-sm font-medium text-center text-white bg-linear-to-r from-brand-500 to-brand-600 rounded-xl hover:from-brand-600 hover:to-brand-700 transition-all"
									>
										Get Started
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
