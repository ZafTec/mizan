"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Search } from "lucide-react";
import { Select } from "@/components/ui/select";

const CATEGORIES = ["Strength", "Cardio", "Flexibility", "Balance"] as const;

/** Filters as you type. The query string is the state, so the page is shareable and the back button works. */
export default function SearchExercises({
  initialSearch,
  initialCategory,
}: {
  initialSearch?: string;
  initialCategory?: string;
}) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [search, setSearch] = useState(initialSearch ?? "");
  const [category, setCategory] = useState(initialCategory ?? "");
  const first = useRef(true);

  useEffect(() => {
    if (first.current) {
      first.current = false;
      return;
    }
    const handle = setTimeout(() => {
      const params = new URLSearchParams();
      if (search.trim()) params.set("search", search.trim());
      if (category) params.set("category", category);
      const query = params.toString();
      startTransition(() =>
        router.replace(query ? `/exercises?${query}` : "/exercises"),
      );
    }, 250);
    return () => clearTimeout(handle);
  }, [search, category, router]);

  return (
    <div className="flex gap-2">
      <label className="relative min-w-0 flex-1">
        <span className="sr-only">Search exercises</span>
        <Search
          aria-hidden="true"
          size={16}
          strokeWidth={1.7}
          className="log-muted pointer-events-none absolute left-3 top-1/2 -translate-y-1/2"
        />
        <input
          type="search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Search exercises"
          className="input min-h-11 pl-9"
        />
      </label>
      <label className="shrink-0">
        <span className="sr-only">Type</span>
        <Select
          value={category}
          onChange={setCategory}
          options={[
            { value: "", label: "Any type" },
            ...CATEGORIES.map((name) => ({ value: name, label: name })),
          ]}
        />
      </label>
    </div>
  );
}
