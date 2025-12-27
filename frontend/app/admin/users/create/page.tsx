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
    <div className="container mx-auto px-4 py-8">
      <div className="mb-8 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold mb-2">Create User</h1>
          <p className="text-muted-foreground">
            Create a new user account with specified role
          </p>
        </div>
        <Link
          href="/admin/users"
          className="px-4 py-2 text-sm border rounded-lg hover:bg-accent"
        >
          ← Back to Users
        </Link>
      </div>

      <div className="max-w-2xl">
        <div className="bg-card rounded-lg border p-6">
          <CreateUserForm />
        </div>
      </div>
    </div>
  );
}
