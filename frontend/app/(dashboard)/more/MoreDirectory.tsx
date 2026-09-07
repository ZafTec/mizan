"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";
import { Search, ArrowRight, X } from "lucide-react";
import type { NavGroup } from "@/components/Layout/nav";

export default function MoreDirectory({ groups }: { groups: NavGroup[] }) {
  const [query, setQuery] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== "/" || event.metaKey || event.ctrlKey || event.altKey)
        return;
      const active = document.activeElement;
      if (
        active instanceof HTMLInputElement ||
        active instanceof HTMLTextAreaElement ||
        (active instanceof HTMLElement && active.isContentEditable)
      )
        return;
      event.preventDefault();
      inputRef.current?.focus();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);
  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return groups
      .map((group) => ({
        ...group,
        items: group.items.filter(
          (item) =>
            !needle ||
            group.label.toLowerCase().includes(needle) ||
            item.label.toLowerCase().includes(needle) ||
            item.description?.toLowerCase().includes(needle) ||
            item.href.toLowerCase().includes(needle),
        ),
      }))
      .filter((group) => group.items.length);
  }, [groups, query]);
  const resultCount = filtered.reduce(
    (total, group) => total + group.items.length,
    0,
  );
  return (
    <div>
      <div className="directory-search">
        <Search size={18} aria-hidden="true" />
        <input
          ref={inputRef}
          type="search"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Find a feature…"
          aria-label="Search features"
          autoComplete="off"
        />
        {query && (
          <button
            className="icon-button"
            aria-label="Clear search"
            onClick={() => {
              setQuery("");
              inputRef.current?.focus();
            }}
          >
            <X size={16} />
          </button>
        )}
      </div>
      {query.trim() && (
        <p aria-live="polite" className="text-sm log-muted mb-6">
          {resultCount} {resultCount === 1 ? "result" : "results"}
        </p>
      )}
      {resultCount ? (
        <div className="feature-directory">
          {filtered.map((group) => (
            <section key={group.label}>
              <h2>{group.label}</h2>
              <div>
                {group.items.map((item) => (
                  <Link
                    key={item.href}
                    href={item.href}
                    className="directory-row"
                  >
                    <span className="min-w-0">
                      <span className="block font-medium">{item.label}</span>
                      {item.description && (
                        <span className="block text-xs log-muted mt-1">
                          {item.description}
                        </span>
                      )}
                    </span>
                    <ArrowRight size={16} aria-hidden="true" />
                  </Link>
                ))}
              </div>
            </section>
          ))}
        </div>
      ) : (
        <div className="log-empty">
          <h2 className="text-xl">No matching features</h2>
          <p className="log-muted text-sm mt-3">
            Try a shorter word, or clear the search to browse everything.
          </p>
          <button
            className="btn-secondary mt-5"
            onClick={() => {
              setQuery("");
              inputRef.current?.focus();
            }}
          >
            Clear search
          </button>
        </div>
      )}
    </div>
  );
}
