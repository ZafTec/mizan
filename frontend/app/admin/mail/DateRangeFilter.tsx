"use client";

import { useTransition } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { withParamsResettingPage } from "@/components/ui/data-table";

/** From and to dates, inclusive, as query parameters the API reads. */
export default function DateRangeFilter() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [, startTransition] = useTransition();

  const set = (name: "from" | "to", value: string) =>
    startTransition(() =>
      router.replace(
        withParamsResettingPage(pathname, new URLSearchParams(params), {
          [name]: value || null,
        }),
        { scroll: false },
      ),
    );

  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      <label className="flex items-center gap-1.5">
        <span className="text-charcoal-blue-500 dark:text-charcoal-blue-400">
          From
        </span>
        <input
          type="date"
          className="input !w-auto !rounded-2xl !py-2 text-sm"
          value={params.get("from") ?? ""}
          onChange={(e) => set("from", e.target.value)}
        />
      </label>
      <label className="flex items-center gap-1.5">
        <span className="text-charcoal-blue-500 dark:text-charcoal-blue-400">
          To
        </span>
        <input
          type="date"
          className="input !w-auto !rounded-2xl !py-2 text-sm"
          value={params.get("to") ?? ""}
          onChange={(e) => set("to", e.target.value)}
        />
      </label>
    </div>
  );
}
