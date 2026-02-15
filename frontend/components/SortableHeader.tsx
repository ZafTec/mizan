import Link from "next/link";

interface SortableHeaderProps {
  children: React.ReactNode;
  sortKey: string;
  currentSort: string | null;
  currentOrder: "asc" | "desc";
  baseUrl: string;
  className?: string;
}

function buildSortUrl(baseUrl: string, sortBy?: string, sortOrder?: string): string {
  const [path, queryString] = baseUrl.split("?");
  const params = new URLSearchParams(queryString || "");
  params.delete("sortBy");
  params.delete("sortOrder");
  params.delete("page");
  if (sortBy) {
    params.set("sortBy", sortBy);
    params.set("sortOrder", sortOrder || "asc");
  }
  const result = params.toString();
  return result ? `${path}?${result}` : path;
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
  const shouldClear = isSorted && currentOrder === "desc";

  const href = shouldClear
    ? buildSortUrl(baseUrl)
    : buildSortUrl(baseUrl, sortKey, isSorted && currentOrder === "asc" ? "desc" : "asc");

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
          <i className="ri-arrow-up-down-line text-slate-400 dark:text-slate-500 text-xs" />
        )}
      </Link>
    </th>
  );
}
