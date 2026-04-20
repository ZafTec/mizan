import { getUserOptionalServer } from "@/helper/session";
import { redirect } from "next/navigation";
import AppShell from "@/components/Layout/AppShell";

export default async function DashboardLayout({
	children,
}: {
	children: React.ReactNode;
}) {
	const user = await getUserOptionalServer();

	if (!user) {
		redirect("/login");
	}

	return <AppShell user={user}>{children}</AppShell>;
}
