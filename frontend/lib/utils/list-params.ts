interface ListParams {
  page?: number;
  pageSize?: number;
  sortBy?: string | null;
  sortOrder?: string | null;
  [key: string]: string | number | null | undefined;
}

export function buildListUrl(basePath: string, params: ListParams): string {
  const searchParams = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (value === null || value === undefined) return;
    if (key === "page" && value === 1) return;
    if (key === "pageSize" && value === 20) return;
    searchParams.set(key, String(value));
  });

  const queryString = searchParams.toString();
  return queryString ? `${basePath}?${queryString}` : basePath;
}

export function parseListParams(
  searchParams: Record<string, string | string[] | undefined>,
  defaults?: {
    pageSize?: number;
    sortBy?: string;
    sortOrder?: "asc" | "desc";
  }
): {
  page: number;
  pageSize: number;
  sortBy: string | null;
  sortOrder: "asc" | "desc";
} {
  const pageParam = searchParams.page;
  const pageSizeParam = searchParams.pageSize;
  const sortByParam = searchParams.sortBy;
  const sortOrderParam = searchParams.sortOrder;

  const page = pageParam
    ? Math.max(1, parseInt(Array.isArray(pageParam) ? pageParam[0] : pageParam, 10))
    : 1;

  const pageSize = pageSizeParam
    ? Math.max(1, parseInt(Array.isArray(pageSizeParam) ? pageSizeParam[0] : pageSizeParam, 10))
    : defaults?.pageSize ?? 20;

  const sortBy = sortByParam
    ? Array.isArray(sortByParam)
      ? sortByParam[0]
      : sortByParam
    : defaults?.sortBy ?? null;

  const sortOrder =
    sortOrderParam && (sortOrderParam === "asc" || sortOrderParam === "desc")
      ? sortOrderParam
      : defaults?.sortOrder ?? "asc";

  return { page, pageSize, sortBy, sortOrder };
}
