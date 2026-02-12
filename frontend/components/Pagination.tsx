import Link from "next/link";
import React from "react";

interface PaginationProps {
  currentPage: number;
  totalPages: number;
  baseUrl?: string;
  onPageChange?: (page: number) => void;
  totalCount?: number;
  pageSize?: number;
}

export default function Pagination({
  currentPage,
  totalPages,
  baseUrl,
  onPageChange,
  totalCount,
  pageSize,
}: PaginationProps) {
  if (totalPages <= 1) return null;

  const getPageUrl = (page: number) => {
    if (!baseUrl) return "#";
    const separator = baseUrl.includes("?") ? "&" : "?";
    return `${baseUrl}${separator}page=${page}`;
  };

  const handlePageClick = (page: number) => {
    if (onPageChange) {
      onPageChange(page);
    }
  };

  const isFirstPage = currentPage === 1;
  const isLastPage = currentPage === totalPages;

  const renderPageButton = (page: number) => {
    const isActive = currentPage === page;
    const className = `w-10 h-10 flex items-center justify-center rounded-xl transition-colors font-medium ${
      isActive
        ? "bg-brand-500 text-white shadow-lg shadow-brand-500/30"
        : "bg-white text-slate-600 hover:bg-slate-50"
    }`;

    if (onPageChange) {
      return (
        <button
          key={page}
          onClick={() => handlePageClick(page)}
          className={className}
        >
          {page}
        </button>
      );
    }

    return (
      <Link key={page} href={getPageUrl(page)} className={className}>
        {page}
      </Link>
    );
  };

  const renderPrevButton = () => {
    const className = `w-10 h-10 flex items-center justify-center rounded-xl bg-slate-100 text-slate-600 transition-colors ${
      isFirstPage ? "opacity-50 pointer-events-none" : "hover:bg-slate-200"
    }`;

    if (onPageChange) {
      return (
        <button
          onClick={() => handlePageClick(Math.max(1, currentPage - 1))}
          className={className}
          disabled={isFirstPage}
        >
          <i className="ri-arrow-left-s-line" />
        </button>
      );
    }

    return (
      <Link
        href={getPageUrl(Math.max(1, currentPage - 1))}
        className={className}
        aria-disabled={isFirstPage}
      >
        <i className="ri-arrow-left-s-line" />
      </Link>
    );
  };

  const renderNextButton = () => {
    const className = `w-10 h-10 flex items-center justify-center rounded-xl bg-slate-100 text-slate-600 transition-colors ${
      isLastPage ? "opacity-50 pointer-events-none" : "hover:bg-slate-200"
    }`;

    if (onPageChange) {
      return (
        <button
          onClick={() => handlePageClick(Math.min(totalPages, currentPage + 1))}
          className={className}
          disabled={isLastPage}
        >
          <i className="ri-arrow-right-s-line" />
        </button>
      );
    }

    return (
      <Link
        href={getPageUrl(Math.min(totalPages, currentPage + 1))}
        className={className}
        aria-disabled={isLastPage}
      >
        <i className="ri-arrow-right-s-line" />
      </Link>
    );
  };

  const showingSummary =
    totalCount !== undefined && pageSize !== undefined ? (
      <div className="text-sm text-slate-500 mb-3 text-center">
        Showing {Math.min((currentPage - 1) * pageSize + 1, totalCount)}-
        {Math.min(currentPage * pageSize, totalCount)} of {totalCount}
      </div>
    ) : null;

  return (
    <div className="mt-8">
      {showingSummary}
      <div className="flex justify-center items-center gap-2">
        {renderPrevButton()}

        <div className="flex items-center gap-1">
          {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => {
            if (
              page === 1 ||
              page === totalPages ||
              (page >= currentPage - 1 && page <= currentPage + 1)
            ) {
              return renderPageButton(page);
            }

            if (page === currentPage - 2 || page === currentPage + 2) {
              return (
                <span key={page} className="text-slate-400 px-1">
                  ...
                </span>
              );
            }

            return null;
          })}
        </div>

        {renderNextButton()}
      </div>
    </div>
  );
}
