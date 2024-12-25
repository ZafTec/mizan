'use client';

import { useEffect, useRef, useState } from "react";


export default function RecipeMoreButton({ recipeId }: { recipeId: string }) {
	const [showMenu, setShowMenu] = useState(false);
	const menuRef = useRef<HTMLDivElement>(null);
    const buttonRef = useRef<HTMLButtonElement>(null);

	function handleClicked(event: React.MouseEvent<HTMLButtonElement>) {
		event.preventDefault();
		event.stopPropagation();

		setShowMenu((prev) => !prev);

		console.log(`More button clicked for recipe ${recipeId}`);
	}

	// Handle clicking outside the menu to close it
	useEffect(() => {
		function handleClickOutside(event: MouseEvent) {
			if (
				menuRef.current && !menuRef.current.contains(event.target as Node) && 
				buttonRef.current && !buttonRef.current.contains(event.target as Node)
			) {
				setShowMenu(false);
			}
		}

		document.addEventListener("mousedown", handleClickOutside);
		return () => {
			document.removeEventListener("mousedown", handleClickOutside);
		};
	}, []);

	return (
		<div className="relative">
			<button
				ref={buttonRef}
				className="text-gray-400 hover:text-gray-600 z-10 hover:bg-emerald-50 w-8 h-8 rounded-full"
				onClick={handleClicked}
				type="button"
				>
				{/* Three dots menu icon */}
				<i className="ri-more-2-line ri-xl font-bold"></i>
			</button>

			{/* dropdown more Menu */}
			{/* Dropdown menu */}
			{showMenu && (
				<div
					ref={menuRef}
					className="absolute bg-white shadow-lg rounded-lg mt-2 z-20 w-20"
				>
					<ul className="text-sm text-gray-700">
						<li>
							<button
								onClick={() => console.log(`Editing recipe ${recipeId}`)}
								className="block px-4 py-2 hover:bg-gray-100 w-full text-left"
							>
								Edit
							</button>
						</li>
						<li>
							<button
								onClick={() => console.log(`Deleting recipe ${recipeId}`)}
								className="block px-4 py-2 hover:bg-gray-100 w-full text-left"
							>
								Delete
							</button>
						</li>
					</ul>
				</div>
			)}
		</div>
	)
}
