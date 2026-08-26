import type { ReactNode } from "react";

/**
 * One column. `cell` receives the row and returns whatever should be in the
 * td - a string, a badge, a set of buttons.
 */
export interface Column<T> {
	id: string;

	header: ReactNode;

	cell: (row: T) => ReactNode;

	/**
	 * The value the API sorts by. Present means the header is a link that
	 * toggles `?sortBy=&sortOrder=`; absent means the header is plain text.
	 * Sorting happens in the database, not in the browser - the page only ever
	 * holds one page of rows, so sorting the array would sort the wrong set.
	 */
	sortKey?: string;

	align?: "left" | "right" | "center";

	/** Hidden below `sm`. For columns that are context rather than content. */
	secondary?: boolean;

	width?: string;
}

export interface TableSort {
	sortBy: string | null;
	sortOrder: "asc" | "desc";
}

export interface TablePage {
	page: number;
	pageSize: number;
	totalCount: number;
	totalPages: number;
}
