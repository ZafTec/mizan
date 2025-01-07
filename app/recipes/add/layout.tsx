'use client';

import { SessionProvider } from "next-auth/react"

export default function AddRecipeLayout({
	children,
	session,
}: Readonly<{ children: React.ReactNode; session: any }>) {
	return (
		<SessionProvider session={session}>
			{children}
		</SessionProvider>
	)
}
