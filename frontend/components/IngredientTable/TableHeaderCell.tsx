'use client';

import React from "react";
import { twMerge } from "tailwind-merge";

export default function TableHeaderCell({ children, className }: Readonly<{ children: React.ReactNode, className?: string }>) {

	return (
		<th className={twMerge("hover:bg-gray-200 dark:hover:bg-slate-700 cursor-pointer w-7", className)}>
			{children}
			<i className="ml-2 ri-arrow-up-down-line"></i>
		</th>
	)
}
