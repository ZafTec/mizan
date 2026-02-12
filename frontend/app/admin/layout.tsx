import { getUserOptionalServer } from "@/helper/session";
import { redirect } from "next/navigation";

export default async function AdminLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    const user = await getUserOptionalServer();

    if (!user) {
        redirect("/login");
    }

    // Enforce admin role verification
    if (user.role !== "admin") {
        redirect("/");
    }

    return <>{children}</>;
}
