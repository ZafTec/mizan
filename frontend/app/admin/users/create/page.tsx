import { redirect } from "next/navigation";
import { auth } from "@/lib/auth";
import Link from "next/link";
import { CreateUserForm } from "./create-user-form";

export const metadata = {
  title: "Create User | Admin",
  description: "Create a new user account",
};

export default async function CreateUserPage() {
  const session = await auth.api.getSession({
    headers: await import("next/headers").then((mod) => mod.headers()),
  });

  if (!session?.user) {
    redirect("/login");
  }

  if (session.user.role !== "admin") {
    redirect("/");
  }

  return (
    <div className="space-y-6 lg:space-y-8">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="space-y-2">
          <p className="eyebrow">Accounts</p>
          <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
            Create user
          </h1>
          <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
            Create a new user account with specified role.
          </p>
        </div>
        <Link
          href="/admin/users"
          className="btn-ghost !rounded-2xl !py-2 text-sm"
        >
          ← Back to Users
        </Link>
      </header>

      <div className="max-w-2xl">
        <div className="glass-panel p-6">
          <CreateUserForm />
        </div>
      </div>
    </div>
  );
}
