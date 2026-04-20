import { getUserOptionalServer } from "@/helper/session";
import { redirect } from "next/navigation";
import AppShell from "@/components/Layout/AppShell";

export default async function AdminLayout({
	children,
}: {
	children: React.ReactNode;
}) {
	const user = await getUserOptionalServer();

	if (!user) {
		redirect("/login");
	}

	if (user.role !== "admin") {
		redirect("/");
	}

	return (
		<AppShell user={user} variant="admin">
			{children}
		</AppShell>
	);
}
