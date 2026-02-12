import Link from "next/link";
import { buildListUrl } from "@/lib/utils/list-params";

interface SortableHeaderProps {
  children: React.ReactNode;
  sortKey: string;
  currentSort: string | null;
  currentOrder: "asc" | "desc";
  baseUrl: string;
  className?: string;
}

export default function SortableHeader({
  children,
  sortKey,
  currentSort,
  currentOrder,
  baseUrl,
  className = "",
}: SortableHeaderProps) {
  const isSorted = currentSort === sortKey;
  const nextOrder = isSorted && currentOrder === "asc" ? "desc" : "asc";

  const href = buildListUrl(baseUrl, {
    sortBy: sortKey,
    sortOrder: nextOrder,
  });

  return (
    <th className={className}>
      <Link href={href} className="inline-flex items-center gap-1">
        <span>{children}</span>
        {isSorted ? (
          <i
            className={
              currentOrder === "asc" ? "ri-arrow-up-s-line" : "ri-arrow-down-s-line"
            }
          />
        ) : (
          <i className="ri-arrow-up-down-line text-slate-400 text-xs" />
        )}
      </Link>
    </th>
  );
}
