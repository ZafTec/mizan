import { getUserOptionalServer } from "@/helper/session";
import { redirect } from "next/navigation";
import AppShell from "@/components/Layout/AppShell";
import AdminTabs from "@/components/admin/AdminTabs";

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
			<div className="space-y-6 lg:space-y-8">
				<AdminTabs />
				{children}
			</div>
		</AppShell>
	);
}
