"use client";

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useState, useEffect, useRef } from 'react';
import { toast } from "sonner";

interface RecipeOptionsProps {
	recipeId: string;
	isCreator: boolean;
}

export default function RecipeOptions({ recipeId, isCreator }: RecipeOptionsProps) {
	const router = useRouter();
	const [showConfirm, setShowConfirm] = useState(false);
	const [showDropdown, setShowDropdown] = useState(false);

	const dropdownRef = useRef<HTMLDivElement>(null);
	const popupRef = useRef<HTMLDivElement>(null);

	// Close dropdown when clicking outside
	useEffect(() => {
		const handleClickOutsideDropdown = (event: MouseEvent) => {
			if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
				setShowDropdown(false);
			}
		};

		if (showDropdown) {
			document.addEventListener('mousedown', handleClickOutsideDropdown);
		} else {
			document.removeEventListener('mousedown', handleClickOutsideDropdown);
		}

		return () => {
			document.removeEventListener('mousedown', handleClickOutsideDropdown);
		};
	}, [showDropdown]);

	// Close popup when clicking outside
	useEffect(() => {
		const handleClickOutsidePopup = (event: MouseEvent) => {
			if (popupRef.current && !popupRef.current.contains(event.target as Node)) {
				setShowConfirm(false);
			}
		};

		if (showConfirm) {
			document.addEventListener('mousedown', handleClickOutsidePopup);
		} else {
			document.removeEventListener('mousedown', handleClickOutsidePopup);
		}

		return () => {
			document.removeEventListener('mousedown', handleClickOutsidePopup);
		};
	}, [showConfirm]);

	const handleDelete = async () => {
		const res = await fetch(`/api/recipes/${recipeId}`, {
			method: 'DELETE',
		});

		if (res.ok) {
			router.push('/recipes');
		} else {
			toast.error('Failed to delete the recipe');
		}
	};

	const confirmDelete = () => {
		setShowConfirm(true);
	};

	const cancelDelete = () => {
		setShowConfirm(false);
	};

	return (
		<div className="relative inline-block text-left">
			<button
				type="button"
				className="px-4 py-2 inline-flex items-center bg-gradient-to-r from-orange-400 to-red-700 text-white font-semibold rounded-full shadow-lg hover:from-orange-500 hover:to-red-900 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
				onClick={() => setShowDropdown(!showDropdown)}
			>
				Options
				<span
					className={`ml-2 transform transition-transform duration-200 ${
						showDropdown ? 'rotate-180' : 'rotate-0'
					}`}
				>
					<svg className="w-4 h-4 inline-block" fill="currentColor" viewBox="0 0 20 20">
						<path
							fillRule="evenodd"
							d="M5.23 7.21a.75.75 0 011.06.02L10 10.94l3.71-3.71a.75.75 0 111.06 1.06l-4.25 4.25a.75.75 0 01-1.06 0L5.21 8.27a.75.75 0 01.02-1.06z"
							clipRule="evenodd"
						/>
					</svg>
				</span>
			</button>

			{/* Options dropdown */}
			{showDropdown && (
				<div
					ref={dropdownRef}
					className="absolute right-0 mt-2 w-56 rounded-md shadow-lg bg-white z-10"
				>
					<div className="py-1">
						<Link
							href={`/meals/add/${recipeId}`}
							className="block px-4 py-2 text-gray-700 hover:bg-gray-100"
						>
							Add to Meal
						</Link>
						<div className="border-t border-gray-200"></div>

						{isCreator && (
							<div>
								<Link
									href={`/recipes/${recipeId}/edit`}
									className="block px-4 py-2 text-gray-700 hover:bg-gray-100"
								>
									Edit
								</Link>
								<button
									onClick={confirmDelete}
									className="block w-full text-left px-4 py-2 text-red-700 hover:bg-gray-100"
								>
									Delete
								</button>
							</div>
						)}
					</div>
				</div>
			)}

			{/* Confirmation popup */}
			{showConfirm && (
				<div className="fixed inset-0 flex items-center justify-center bg-gray-500 bg-opacity-75 z-50">
					<div ref={popupRef} className="bg-white p-4 rounded shadow-lg">
						<p>Are you sure you want to delete this recipe?</p>
						<div className="mt-4 flex justify-end space-x-2 text-white text-sm">
							<button
								className="bg-slate-500 px-5 py-2 rounded hover:bg-slate-700"
								onClick={cancelDelete}
							>
								Cancel
							</button>
							<button
								className="bg-red-600 px-5 py-2 rounded hover:bg-red-800"
								onClick={handleDelete}
							>
								Delete
							</button>
						</div>
					</div>
				</div>
			)}
		</div>
	)
}
