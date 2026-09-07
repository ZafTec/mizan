import { getUserOptionalServer } from "@/helper/session";
import AppShell from "@/components/Layout/AppShell";

export default async function RecipeLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const user = await getUserOptionalServer();
  return user ? <AppShell user={user}>{children}</AppShell> : children;
}
