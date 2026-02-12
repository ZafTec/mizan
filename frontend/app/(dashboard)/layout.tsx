import { getUserOptionalServer } from "@/helper/session";
import { redirect } from "next/navigation";

export default async function DashboardLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    const user = await getUserOptionalServer();

    if (!user) {
        redirect("/login");
    }

    return <>{children}</>;
}
