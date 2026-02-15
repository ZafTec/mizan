'use client';

import type { User } from "@/lib/auth";
import Link from "next/link";
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
					? 'text-brand-600 bg-brand-50'
					: 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
			}`}
		>
			{children}
		</Link>
	);
};

export default function NavbarContent({ user }: NavbarContentProps) {
	const [menuOpen, setMenuOpen] = useState(false);
	const [showLogoutModal, setShowLogoutModal] = useState(false);
	const menuRef = useRef<HTMLDivElement>(null);
	const router = useRouter();

	useEffect(() => {
		const handleClickOutside = (event: MouseEvent) => {
			if (
				menuRef.current &&
				!menuRef.current.contains(event.target as Node) &&
				!(event.target as HTMLElement).closest("button[aria-label='Toggle navigation menu']")
			) {
				setMenuOpen(false);
			}
		};

		if (menuOpen) {
			document.addEventListener("mousedown", handleClickOutside);
		}

		return () => {
			document.removeEventListener("mousedown", handleClickOutside);
		};
	}, [menuOpen]);

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
				<div className="bg-white rounded-2xl p-6 max-w-sm w-full shadow-xl" onClick={(e) => e.stopPropagation()}>
					<div className="flex items-center gap-3 mb-4">
						<div className="w-10 h-10 rounded-xl bg-red-100 flex items-center justify-center">
							<i className="ri-logout-box-r-line text-xl text-red-600" />
						</div>
						<h3 className="text-lg font-semibold text-slate-900">Sign Out</h3>
					</div>
					<p className="text-sm text-slate-500 mb-6">Are you sure you want to sign out?</p>
					<div className="flex gap-3">
						<button
							onClick={() => setShowLogoutModal(false)}
							className="flex-1 px-4 py-2.5 text-sm font-medium text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-xl transition-colors"
						>
							Cancel
						</button>
						<button
							onClick={() => { setShowLogoutModal(false); handleLogout(); }}
							className="flex-1 px-4 py-2.5 text-sm font-medium text-white bg-red-600 hover:bg-red-700 rounded-xl transition-colors"
						>
							Sign Out
						</button>
					</div>
				</div>
			</div>
		)}
		<div className="flex items-center gap-2">
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
				{user?.role === 'admin' && (
					<NavLink href="/admin">
						<i className="ri-shield-user-line mr-1.5" />
						Admin
					</NavLink>
				)}
			</nav>

			{/* User Actions */}
			<div className="hidden md:flex items-center gap-2 ml-4 pl-4 border-l border-slate-200">
				{user ? (
					<>
						<Link
							href="/profile"
							className="flex items-center gap-2 px-3 py-2 text-sm text-slate-600 hover:text-slate-900 hover:bg-slate-100 rounded-lg transition-colors"
						>
							<div className="w-8 h-8 rounded-full bg-linear-to-br from-brand-400 to-brand-600 flex items-center justify-center text-white text-sm font-medium">
								{user.email?.charAt(0).toUpperCase() || 'U'}
							</div>
						</Link>
						<button
							onClick={() => setShowLogoutModal(true)}
							className="px-3 py-2 text-sm text-slate-500 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors"
						>
							<i className="ri-logout-box-r-line text-lg" />
						</button>
					</>
				) : (
					<>
						<Link
							href="/login"
							className="px-4 py-2 text-sm font-medium text-slate-600 hover:text-slate-900 transition-colors"
						>
							Sign In
						</Link>
						<Link
							href="/register"
							className="px-4 py-2 text-sm font-medium text-white bg-linear-to-r from-brand-500 to-brand-600 rounded-xl hover:from-brand-600 hover:to-brand-700 shadow-lg shadow-brand-500/25 transition-all"
						>
							Get Started
						</Link>
					</>
				)}
			</div>

			{/* Mobile Menu Button */}
			<button
				className="md:hidden p-2 text-slate-600 hover:text-slate-900 hover:bg-slate-100 rounded-lg transition-colors"
				onClick={() => setMenuOpen(!menuOpen)}
				aria-label="Toggle navigation menu"
				aria-expanded={menuOpen}
			>
				<svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
					{menuOpen ? (
						<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
					) : (
						<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
					)}
				</svg>
			</button>

			{/* Mobile Menu */}
			{menuOpen && (
				<div
					ref={menuRef}
					className="absolute top-full left-0 right-0 mt-2 mx-4 p-4 bg-white rounded-2xl shadow-xl border border-slate-200 md:hidden animate-fade-in"
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
						{user?.role === 'admin' && (
							<NavLink href="/admin" onClick={closeMenu}>
								<i className="ri-shield-user-line mr-2" />
								Admin
							</NavLink>
						)}
					</nav>

					<div className="mt-4 pt-4 border-t border-slate-200">
						{user ? (
							<div className="flex items-center justify-between">
								<Link
									href="/profile"
									onClick={closeMenu}
									className="flex items-center gap-3 px-3 py-2 text-sm text-slate-600 hover:bg-slate-100 rounded-lg"
								>
									<div className="w-8 h-8 rounded-full bg-linear-to-br from-brand-400 to-brand-600 flex items-center justify-center text-white text-sm font-medium">
										{user.email?.charAt(0).toUpperCase() || 'U'}
									</div>
									<span>Profile</span>
								</Link>
								<button
									onClick={() => { closeMenu(); setShowLogoutModal(true); }}
									className="px-4 py-2 text-sm text-red-600 hover:bg-red-50 rounded-lg transition-colors"
								>
									Sign Out
								</button>
							</div>
						) : (
							<div className="flex gap-2">
								<Link
									href="/login"
									onClick={closeMenu}
									className="flex-1 px-4 py-2.5 text-sm font-medium text-center text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200 transition-colors"
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
			)}
		</div>
		</>
	);
}
