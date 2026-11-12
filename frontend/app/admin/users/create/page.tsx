import { redirect } from "next/navigation";
import { getCurrentUser } from "@/lib/auth";
import AdminPageHeader from "@/components/admin/AdminPageHeader";
import { CreateUserForm } from "./create-user-form";

export const metadata = {
  title: "New user | Admin",
  description: "Create a new user account",
};

export default async function CreateUserPage() {
  const user = await getCurrentUser();
  if (!user) redirect("/login");
  if (user.role !== "admin") redirect("/");

  return (
    <>
      <AdminPageHeader
        title="New user"
        lead="The account can sign in at once with this password; no verification email is sent."
        back={{ href: "/admin/users", label: "Users" }}
      />
      <div className="max-w-2xl">
        <CreateUserForm />
      </div>
    </>
  );
}
