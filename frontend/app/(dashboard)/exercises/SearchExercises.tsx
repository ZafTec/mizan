"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useState, useTransition } from "react";

interface SearchExercisesProps {
    initialSearch?: string;
    initialCategory?: string;
}

export default function SearchExercises({ initialSearch, initialCategory }: SearchExercisesProps) {
    const router = useRouter();
    const searchParams = useSearchParams();
    const [isPending, startTransition] = useTransition();
    const [search, setSearch] = useState(initialSearch || "");
    const [category, setCategory] = useState(initialCategory || "");

    const handleSearch = () => {
        const params = new URLSearchParams(searchParams);
        if (search) {
            params.set("search", search);
        } else {
            params.delete("search");
        }
        if (category) {
            params.set("category", category);
        } else {
            params.delete("category");
        }

        startTransition(() => {
            router.push(`/exercises?${params.toString()}`);
        });
    };

    const handleClear = () => {
        setSearch("");
        setCategory("");
        startTransition(() => {
            router.push("/exercises");
        });
    };

    return (
        <div className="card p-6">
            <div className="flex flex-col sm:flex-row gap-4">
                <div className="flex-1">
                    <input
                        type="text"
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                        onKeyDown={(e) => e.key === "Enter" && handleSearch()}
                        placeholder="Search exercises..."
                        className="input"
                    />
                </div>
                <div className="sm:w-48">
                    <select
                        value={category}
                        onChange={(e) => setCategory(e.target.value)}
                        className="input"
                    >
                        <option value="">All Categories</option>
                        <option value="Strength">Strength</option>
                        <option value="Cardio">Cardio</option>
                        <option value="Flexibility">Flexibility</option>
                        <option value="Balance">Balance</option>
                    </select>
                </div>
                <div className="flex gap-2">
                    <button
                        onClick={handleSearch}
                        disabled={isPending}
                        className="btn-primary whitespace-nowrap"
                    >
                        <i className="ri-search-line" />
                        Search
                    </button>
                    {(search || category) && (
                        <button
                            onClick={handleClear}
                            disabled={isPending}
                            className="btn-secondary"
                        >
                            <i className="ri-close-line" />
                            Clear
                        </button>
                    )}
                </div>
            </div>
        </div>
    );
}
