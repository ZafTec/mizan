import { redirect } from "next/navigation";
import { getUserOptionalServer } from "@/helper/session";
import { getAdminOverviewServer } from "@/lib/api/admin-ops.server";
import AdminShell from "@/components/admin/AdminShell";

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

  const overview = await getAdminOverviewServer();

  return (
    <AdminShell attention={overview?.attention ?? {}}>{children}</AdminShell>
  );
}
