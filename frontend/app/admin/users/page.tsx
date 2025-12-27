import { redirect } from "next/navigation";
import { auth } from "@/lib/auth";
import { db } from "@/db/client";
import { users } from "@/db/schema";
import { eq, ilike, and, sql, count, desc } from "drizzle-orm";
import Link from "next/link";

export const metadata = {
  title: "User Management | Admin",
  description: "Manage system users",
};

interface SearchParams {
  page?: string;
  search?: string;
  role?: string;
  banned?: string;
}

const USERS_PER_PAGE = 20;

async function getUsers(searchParams: SearchParams) {
  const page = parseInt(searchParams.page || "1");
  const search = searchParams.search || "";
  const roleFilter = searchParams.role;
  const bannedFilter = searchParams.banned === "true";

  const conditions = [];

  if (search) {
    conditions.push(
      sql`(${ilike(users.name, `%${search}%`)} OR ${ilike(
        users.email,
        `%${search}%`
      )})`
    );
  }

  if (roleFilter) {
    conditions.push(eq(users.role, roleFilter));
  }

  if (bannedFilter) {
    conditions.push(eq(users.banned, true));
  }

  const whereClause = conditions.length > 0 ? and(...conditions) : undefined;

  const [userList, totalCount] = await Promise.all([
    db
      .select({
        id: users.id,
        name: users.name,
        email: users.email,
        role: users.role,
        banned: users.banned,
        banReason: users.banReason,
        emailVerified: users.emailVerified,
        createdAt: users.createdAt,
      })
      .from(users)
      .where(whereClause)
      .orderBy(desc(users.createdAt))
      .limit(USERS_PER_PAGE)
      .offset((page - 1) * USERS_PER_PAGE),

    db
      .select({ count: count() })
      .from(users)
      .where(whereClause)
      .then((res) => res[0]?.count || 0),
  ]);

  return {
    users: userList,
    totalCount,
    totalPages: Math.ceil(totalCount / USERS_PER_PAGE),
    currentPage: page,
  };
}

export default async function UsersPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const session = await auth.api.getSession({
    headers: await import("next/headers").then((mod) => mod.headers()),
  });

  if (!session?.user) {
    redirect("/login");
  }

  if (session.user.role !== "admin") {
    redirect("/");
  }

  const { users: userList, totalCount, totalPages, currentPage } = await getUsers(params);

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="mb-8 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold mb-2">User Management</h1>
          <p className="text-muted-foreground">
            {totalCount} total users
          </p>
        </div>
        <div className="flex gap-3">
          <Link
            href="/admin/users/create"
            className="px-4 py-2 text-sm bg-primary text-primary-foreground rounded-lg hover:bg-primary/90"
          >
            + Create User
          </Link>
          <Link
            href="/admin"
            className="px-4 py-2 text-sm border rounded-lg hover:bg-accent"
          >
            ← Back to Dashboard
          </Link>
        </div>
      </div>

      <div className="mb-6 bg-card rounded-lg border p-4">
        <form className="flex flex-col md:flex-row gap-4">
          <input
            type="text"
            name="search"
            placeholder="Search by name or email..."
            defaultValue={params.search}
            className="flex-1 px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
          />
          <select
            name="role"
            defaultValue={params.role || ""}
            className="px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
          >
            <option value="">All Roles</option>
            <option value="user">User</option>
            <option value="trainer">Trainer</option>
            <option value="admin">Admin</option>
          </select>
          <select
            name="banned"
            defaultValue={params.banned || ""}
            className="px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
          >
            <option value="">All Status</option>
            <option value="true">Banned Only</option>
          </select>
          <button
            type="submit"
            className="px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90"
          >
            Filter
          </button>
          <Link
            href="/admin/users"
            className="px-6 py-2 border rounded-lg hover:bg-accent text-center"
          >
            Clear
          </Link>
        </form>
      </div>

      <div className="bg-card rounded-lg border overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-muted">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  User
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Role
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Status
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Verified
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Joined
                </th>
                <th className="px-6 py-3 text-right text-xs font-medium uppercase tracking-wider">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {userList.map((user) => (
                <tr key={user.id} className="hover:bg-muted/50">
                  <td className="px-6 py-4">
                    <div>
                      <p className="font-medium">{user.name || "Unnamed"}</p>
                      <p className="text-sm text-muted-foreground">
                        {user.email}
                      </p>
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    <span
                      className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${user.role === "admin"
                        ? "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200"
                        : user.role === "trainer"
                          ? "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200"
                          : "bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200"
                        }`}
                    >
                      {user.role}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    {user.banned ? (
                      <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200">
                        Banned
                      </span>
                    ) : (
                      <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200">
                        Active
                      </span>
                    )}
                  </td>
                  <td className="px-6 py-4">
                    {user.emailVerified ? (
                      <span className="text-green-600">✓</span>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </td>
                  <td className="px-6 py-4 text-sm text-muted-foreground">
                    {user.createdAt
                      ? new Date(user.createdAt).toLocaleDateString()
                      : "—"}
                  </td>
                  <td className="px-6 py-4 text-right">
                    <Link
                      href={`/admin/users/${user.id}`}
                      className="text-primary hover:underline text-sm"
                    >
                      View Details →
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {totalPages > 1 && (
        <div className="mt-6 flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Page {currentPage} of {totalPages}
          </p>
          <div className="flex gap-2">
            {currentPage > 1 && (
              <Link
                href={`/admin/users?${new URLSearchParams({
                  ...params,
                  page: (currentPage - 1).toString(),
                }).toString()}`}
                className="px-4 py-2 border rounded-lg hover:bg-accent"
              >
                ← Previous
              </Link>
            )}
            {currentPage < totalPages && (
              <Link
                href={`/admin/users?${new URLSearchParams({
                  ...params,
                  page: (currentPage + 1).toString(),
                }).toString()}`}
                className="px-4 py-2 border rounded-lg hover:bg-accent"
              >
                Next →
              </Link>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
