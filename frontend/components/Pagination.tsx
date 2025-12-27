import Link from 'next/link';
import React from 'react';

interface PaginationProps {
    currentPage: number;
    totalPages: number;
    baseUrl: string;
}

export default function Pagination({ currentPage, totalPages, baseUrl }: PaginationProps) {
    if (totalPages <= 1) return null;

    // Function to generate the URL for a specific page
    const getPageUrl = (page: number) => {
        // Preserve other query params if tricky, but simplistic approach:
        // If baseUrl contains ?, append &page=, else ?page=
        const separator = baseUrl.includes('?') ? '&' : '?';
        return `${baseUrl}${separator}page=${page}`;
    };

    return (
        <div className="flex justify-center items-center gap-2 mt-8">
            {/* Prev Button */}
            <Link
                href={getPageUrl(Math.max(1, currentPage - 1))}
                className={`w-10 h-10 flex items-center justify-center rounded-xl bg-slate-100 text-slate-600 transition-colors ${currentPage === 1 ? 'opacity-50 pointer-events-none' : 'hover:bg-slate-200'
                    }`}
                aria-disabled={currentPage === 1}
            >
                <i className="ri-arrow-left-s-line" />
            </Link>

            {/* Pages */}
            <div className="flex items-center gap-1">
                {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => {
                    // Simple truncation logic could be added here if needed, but for now show all or simple list
                    // For simplicity, lets show current, first, last and neighbors.
                    // However, to keep it clean for now, just standard list for small counts, 
                    // In real apps, complex logic is needed. Let's just do a simple list max 5-7.

                    if (
                        page === 1 ||
                        page === totalPages ||
                        (page >= currentPage - 1 && page <= currentPage + 1)
                    ) {
                        return (
                            <Link
                                key={page}
                                href={getPageUrl(page)}
                                className={`w-10 h-10 flex items-center justify-center rounded-xl transition-colors font-medium ${currentPage === page
                                        ? 'bg-brand-500 text-white shadow-lg shadow-brand-500/30'
                                        : 'bg-white text-slate-600 hover:bg-slate-50'
                                    }`}
                            >
                                {page}
                            </Link>
                        );
                    }

                    if (
                        page === currentPage - 2 ||
                        page === currentPage + 2
                    ) {
                        return <span key={page} className="text-slate-400 px-1">...</span>;
                    }

                    return null;
                })}
            </div>

            {/* Next Button */}
            <Link
                href={getPageUrl(Math.min(totalPages, currentPage + 1))}
                className={`w-10 h-10 flex items-center justify-center rounded-xl bg-slate-100 text-slate-600 transition-colors ${currentPage === totalPages ? 'opacity-50 pointer-events-none' : 'hover:bg-slate-200'
                    }`}
                aria-disabled={currentPage === totalPages}
            >
                <i className="ri-arrow-right-s-line" />
            </Link>
        </div>
    );
}
