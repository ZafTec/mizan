
'use client';

import User from "@/types/user";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";

interface NavbarContentProps {
	user: User | null;
}
export default function NavbarContent({ user }: NavbarContentProps) {
	const [menuOpen, setMenuOpen] = useState(false);
	const menuRef = useRef<HTMLUListElement>(null);
  
	// Close menu when clicking outside
	useEffect(() => {
		const handleClickOutside = (event: MouseEvent) => {
			if (
				menuRef.current &&
				!menuRef.current.contains(event.target as Node) &&
				!(event.target as HTMLElement).closest("button[aria-label='Toggle navigation menu']")
			) {
				setMenuOpen(false);
			}
		}
	
		if (menuOpen) {
			document.addEventListener("mousedown", handleClickOutside);
		} else {
			document.removeEventListener("mousedown", handleClickOutside);
		}
	
		return () => {
			document.removeEventListener("mousedown", handleClickOutside);
		};
	}, [menuOpen]);
  
	return (
		<div className="relative flex justify-end w-full md:w-auto">

			{/* Hamburger menu button for mobile */}
			<button
				className="text-white focus:outline-none md:hidden z-20"
				onClick={() => setMenuOpen(!menuOpen)}
				aria-label="Toggle navigation menu"
				aria-expanded={menuOpen}
				aria-controls="primary-navigation"
			>
			{/* Hamburger icon */}
				<svg
					className="w-6 h-6"
					fill="none"
					stroke="currentColor"
					viewBox="0 0 24 24"
				>
					{menuOpen ? (
						// Close icon
						<path
							strokeLinecap="round"
							strokeLinejoin="round"
							strokeWidth={2}
							d="M6 18L18 6M6 6l12 12"
						/>
					) : (
					// Menu icon
						<path
							strokeLinecap="round"
							strokeLinejoin="round"
							strokeWidth={2}
							d="M4 6h16M4 12h16M4 18h16"
						/>
					)}
				</svg>
			</button>

			{/* Navigation links */}
			<ul
				id="primary-navigation"
				ref={menuRef}
				className={`${menuOpen ? "block" : "hidden"} absolute top-8 z-10 shadow-lg md:shadow-none rounded-md w-full bg-emerald-600 md:bg-transparent md:static md:flex md:items-center md:w-auto`}
			>
				<li className="md:ml-4">
					<Link
						href="/ingredients"
						className="block px-4 py-2 rounded-t-md text-white hover:bg-emerald-500 md:hover:bg-transparent md:inline-block md:px-2 md:py-0"
					>
						Ingredients
					</Link>
				</li>
				<li className="md:ml-4">
					<Link
						href="/recipes"
						className="block px-4 py-2 text-white hover:bg-emerald-500 md:hover:bg-transparent md:inline-block md:px-2 md:py-0"
					>
						Recipes
					</Link>
				</li>

				{user ? (
					<>
						<li className="md:ml-4">
							<Link
								href="/meals"
								className="block px-4 py-2 text-white hover:bg-emerald-500 md:hover:bg-transparent md:inline-block md:px-2 md:py-0"
							>
							Meals
							</Link>
						</li>
						<li className="md:ml-4">
							<Link
								href="/profile"
								className="block px-4 py-2 text-white hover:bg-emerald-600 md:hover:bg-transparent md:inline-block md:px-2 md:py-0"
							>
								Profile
							</Link>
						</li>
						<li className="md:ml-4">
							<Link
								href="/api/auth/logout?callbackUrl=/"
								className="block px-4 py-2 rounded-b-md text-white hover:bg-emerald-500 md:hover:bg-transparent md:inline-block md:px-2 md:py-0"
							>
								Sign Out
							</Link>
						</li>
					</>
				) : (
					<li className="md:ml-4">
						<Link
							href="/login"
							className="block px-4 py-2 rounded-b-md text-white hover:bg-emerald-500 md:hover:bg-transparent md:inline-block md:px-2 md:py-0"
						>
							Sign In
						</Link>
					</li>
				)}
			</ul>

		</div>
	)
}
