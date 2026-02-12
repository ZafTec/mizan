"use client";

import { useSearchParams, useRouter, usePathname } from "next/navigation";
import { useCallback, useMemo } from "react";

interface UseListStateOptions<TFilters extends Record<string, string>> {
  defaultPageSize?: number;
  defaultSortBy?: string;
  defaultSortOrder?: "asc" | "desc";
  filterKeys?: (keyof TFilters)[];
}

interface ListState<TFilters extends Record<string, string>> {
  page: number;
  pageSize: number;
  sortBy: string | null;
  sortOrder: "asc" | "desc";
  filters: Partial<TFilters>;
  setPage: (page: number) => void;
  setSort: (sortBy: string) => void;
  setFilter: (key: keyof TFilters, value: string | null) => void;
  resetFilters: () => void;
}

export function useListState<TFilters extends Record<string, string>>({
  defaultPageSize = 20,
  defaultSortBy,
  defaultSortOrder = "asc",
  filterKeys = [],
}: UseListStateOptions<TFilters> = {}): ListState<TFilters> {
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  const page = useMemo(() => {
    const pageParam = searchParams.get("page");
    return pageParam ? Math.max(1, parseInt(pageParam, 10)) : 1;
  }, [searchParams]);

  const pageSize = useMemo(() => {
    const pageSizeParam = searchParams.get("pageSize");
    return pageSizeParam ? Math.max(1, parseInt(pageSizeParam, 10)) : defaultPageSize;
  }, [searchParams, defaultPageSize]);

  const sortBy = useMemo(() => {
    return searchParams.get("sortBy") ?? defaultSortBy ?? null;
  }, [searchParams, defaultSortBy]);

  const sortOrder = useMemo(() => {
    const orderParam = searchParams.get("sortOrder");
    return orderParam === "desc" ? "desc" : defaultSortOrder;
  }, [searchParams, defaultSortOrder]);

  const filters = useMemo(() => {
    const result: Partial<TFilters> = {};
    filterKeys.forEach((key) => {
      const value = searchParams.get(String(key));
      if (value) {
        result[key] = value as TFilters[keyof TFilters];
      }
    });
    return result;
  }, [searchParams, filterKeys]);

  const updateParams = useCallback(
    (updates: Record<string, string | null>) => {
      const params = new URLSearchParams(searchParams.toString());

      Object.entries(updates).forEach(([key, value]) => {
        if (value === null || value === "") {
          params.delete(key);
        } else {
          params.set(key, value);
        }
      });

      const queryString = params.toString();
      const newUrl = queryString ? `${pathname}?${queryString}` : pathname;
      router.replace(newUrl);
    },
    [searchParams, pathname, router]
  );

  const setPage = useCallback(
    (newPage: number) => {
      updateParams({ page: newPage === 1 ? null : String(newPage) });
    },
    [updateParams]
  );

  const setSort = useCallback(
    (newSortBy: string) => {
      const newSortOrder =
        sortBy === newSortBy && sortOrder === "asc" ? "desc" : "asc";
      updateParams({
        sortBy: newSortBy,
        sortOrder: newSortOrder,
        page: null,
      });
    },
    [sortBy, sortOrder, updateParams]
  );

  const setFilter = useCallback(
    (key: keyof TFilters, value: string | null) => {
      updateParams({
        [String(key)]: value,
        page: null,
      });
    },
    [updateParams]
  );

  const resetFilters = useCallback(() => {
    const updates: Record<string, string | null> = { page: null };
    filterKeys.forEach((key) => {
      updates[String(key)] = null;
    });
    updateParams(updates);
  }, [filterKeys, updateParams]);

  return {
    page,
    pageSize,
    sortBy,
    sortOrder,
    filters,
    setPage,
    setSort,
    setFilter,
    resetFilters,
  };
}

export { parseListParams } from "../utils/list-params";
