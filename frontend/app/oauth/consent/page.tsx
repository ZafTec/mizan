import { Suspense } from "react";
import type { Metadata } from "next";
import { ConsentScreen } from "./ConsentScreen";
import { Skeleton } from "@/components/ui/skeleton";

export const metadata: Metadata = {
	title: "Connect an app | Mizan",
	robots: { index: false },
};

export default function ConsentPage() {
	return (
		<Suspense fallback={<Skeleton className="mx-auto h-96 max-w-xl" />}>
			<ConsentScreen />
		</Suspense>
	);
}
