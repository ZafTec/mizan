import Loading from "@/components/Loading";
import { Suspense } from "react";


export default function Layout(
	{ children }: Readonly<{ children: React.ReactNode }>
) {
	return (
		<Suspense fallback={<Loading />}>
			{children}
		</Suspense>
	)
}
