"use client";

import { useEffect, useId, useState } from "react";
import { Search, X } from "lucide-react";
import { clientApi } from "@/lib/api.client";
import type { components } from "@/types/api.generated";
import type { FoodChoice } from "@/lib/planning/rules";

type FoodPage = components["schemas"]["FoodDtoPagedResult"];

/**
 * A list of foods a rule names, with a search to add one. The rules hold
 * ids; the names shown are the ones the search returned.
 */
export default function FoodSearch({
  label,
  value,
  onChange,
  error,
}: {
  label: string;
  value: FoodChoice[];
  onChange: (next: FoodChoice[]) => void;
  error?: string;
}) {
  const id = useId();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<FoodChoice[] | null>(null);

  useEffect(() => {
    const term = query.trim();
    if (!term) return;
    let active = true;
    const timer = window.setTimeout(async () => {
      try {
        const page = await clientApi<FoodPage>(
          `/api/Foods/search?SearchTerm=${encodeURIComponent(term)}&PageSize=8`,
        );
        if (active)
          setResults(
            page.items.map((food) => ({ id: food.id, name: food.name })),
          );
      } catch {
        if (active) setResults([]);
      }
    }, 200);
    return () => {
      active = false;
      window.clearTimeout(timer);
    };
  }, [query]);

  const add = (food: FoodChoice) => {
    if (!value.some((item) => item.id === food.id)) onChange([...value, food]);
    setQuery("");
    setResults(null);
  };

  return (
    <div>
      <label htmlFor={id} className="label">
        {label}
      </label>
      {value.length > 0 && (
        <ul className="mb-2 flex flex-wrap gap-2" aria-label={label}>
          {value.map((food) => (
            <li
              key={food.id}
              className="flex items-center gap-1 border border-border px-2 py-1 text-sm"
            >
              {food.name}
              <button
                type="button"
                aria-label={`Remove ${food.name}`}
                className="icon-button h-6! w-6!"
                onClick={() =>
                  onChange(value.filter((item) => item.id !== food.id))
                }
              >
                <X size={14} strokeWidth={1.7} aria-hidden="true" />
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="relative">
        <Search
          aria-hidden="true"
          size={16}
          strokeWidth={1.7}
          className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 log-muted"
        />
        <input
          id={id}
          type="search"
          value={query}
          onChange={(event) => {
            setQuery(event.target.value);
            if (!event.target.value.trim()) setResults(null);
          }}
          onKeyDown={(event) => {
            if (event.key === "Enter") event.preventDefault();
          }}
          placeholder="Search foods"
          autoComplete="off"
          className="input w-full pl-9"
          aria-invalid={error ? true : undefined}
        />
      </div>
      {results && (
        <ul className="divide-y divide-border border-b border-border">
          {results.length === 0 ? (
            <li className="py-2 text-sm log-muted">No foods match.</li>
          ) : (
            results.map((food) => (
              <li key={food.id}>
                <button
                  type="button"
                  className="flex min-h-11 w-full items-center text-left text-sm hover:bg-muted"
                  onClick={() => add(food)}
                >
                  Add {food.name}
                </button>
              </li>
            ))
          )}
        </ul>
      )}
      {error && <p className="input-error mt-1 text-sm">{error}</p>}
    </div>
  );
}
