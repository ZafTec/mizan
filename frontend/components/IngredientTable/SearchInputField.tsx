'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';

export default function SearchBar() {
	const router = useRouter();
	const [searchTerm, setSearchTerm] = useState('');

	function handleSearch(event: React.KeyboardEvent<HTMLInputElement>) {
		if (event.key === 'Enter') {
			router.push(searchTerm.length ? `?searchIngredient=${encodeURIComponent(searchTerm)}` : '?');
		}
	}

	return (
		<input
			type="text"
			placeholder="Search ingredient"
			className="border-2 border-gray-300 rounded-lg p-2"
			value={searchTerm}
			onChange={(e) => setSearchTerm(e.target.value)}
			onKeyDown={handleSearch}
		/>
	)
}
