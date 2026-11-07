"use client";

import { useState, useTransition } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Icon } from "@/components/ui/icon";
import { withParamsResettingPage } from "@/components/ui/data-table";
import { resolvePublicApiOrigin } from "@/lib/api-base";
import { Select } from "@/components/ui/select";

/**
 * The audit log needs more than the shared toolbar offers: two date bounds and
 * an export of whatever is currently filtered. Everything still ends up in the
 * query string, so the page stays server-rendered and a filtered view is a URL.
 */
export default function AuditToolbar({
  actions,
  entityTypes,
  exportQuery,
}: {
  actions: string[];
  entityTypes: string[];
  exportQuery: string;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [, startTransition] = useTransition();
  const [term, setTerm] = useState(params.get("search") ?? "");

  function set(changes: Record<string, string | null>) {
    startTransition(() => {
      router.replace(
        withParamsResettingPage(
          pathname,
          new URLSearchParams(params.toString()),
          changes,
        ),
        { scroll: false },
      );
    });
  }

  const anyFilter = ["action", "entityType", "search", "from", "to"].some((k) =>
    params.get(k),
  );

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            set({ search: term || null });
          }}
          className="relative min-w-0 flex-1 sm:max-w-xs"
        >
          <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-charcoal-blue-400">
            <Icon name="search" size={14} />
          </span>
          <input
            type="search"
            value={term}
            onChange={(e) => setTerm(e.target.value)}
            placeholder="Actor email or entity id…"
            aria-label="Search the audit log"
            className="input w-full !rounded-2xl !py-2 !pl-9 text-sm"
          />
        </form>

        <Select
          value={params.get("action") ?? ""}
          onChange={(next) => set({ action: next || null })}
          aria-label="Action"
          className="w-auto text-sm"
          options={[
            { value: "", label: "Any action" },
            ...actions.map((a) => ({ value: a, label: a })),
          ]}
        />

        <Select
          value={params.get("entityType") ?? ""}
          onChange={(next) => set({ entityType: next || null })}
          aria-label="Entity type"
          className="w-auto text-sm"
          options={[
            { value: "", label: "Any entity" },
            ...entityTypes.map((t) => ({ value: t, label: t })),
          ]}
        />

        <a
          href={`${resolvePublicApiOrigin()}/api/AuditLogs/export?${exportQuery}`}
          className="btn-secondary btn-sm !rounded-2xl"
          // Same-origin download is blocked by the API subdomain, so this
          // is a plain navigation the browser saves.
          download
        >
          Export CSV
        </a>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <label className="flex items-center gap-2 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
          From
          <input
            type="date"
            value={params.get("from") ?? ""}
            max={params.get("to") ?? undefined}
            onChange={(e) => set({ from: e.target.value || null })}
            className="input !w-auto !rounded-2xl !py-1.5 text-sm"
          />
        </label>
        <label className="flex items-center gap-2 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
          To
          <input
            type="date"
            value={params.get("to") ?? ""}
            min={params.get("from") ?? undefined}
            onChange={(e) => set({ to: e.target.value || null })}
            className="input !w-auto !rounded-2xl !py-1.5 text-sm"
          />
        </label>

        {anyFilter && (
          <button
            type="button"
            onClick={() => {
              setTerm("");
              startTransition(() =>
                router.replace(pathname, { scroll: false }),
              );
            }}
            className="btn-ghost btn-sm"
          >
            Clear
          </button>
        )}
      </div>
    </div>
  );
}
