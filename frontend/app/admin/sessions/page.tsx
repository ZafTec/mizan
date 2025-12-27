import { redirect } from "next/navigation";
import { auth } from "@/lib/auth";
import { db } from "@/db/client";
import { sessions, users } from "@/db/schema";
import { eq, sql, count, desc } from "drizzle-orm";
import Link from "next/link";

export const metadata = {
  title: "Session Management | Admin",
  description: "Manage active user sessions",
};

interface SearchParams {
  page?: string;
  activeOnly?: string;
}

const SESSIONS_PER_PAGE = 50;

async function getSessions(searchParams: SearchParams) {
  const page = parseInt(searchParams.page || "1");
  const activeOnly = searchParams.activeOnly === "true";

  const conditions = activeOnly
    ? sql`${sessions.expiresAt} > NOW()`
    : undefined;

  const [sessionList, totalCount] = await Promise.all([
    db
      .select({
        id: sessions.id,
        userId: sessions.userId,
        userName: users.name,
        userEmail: users.email,
        ipAddress: sessions.ipAddress,
        userAgent: sessions.userAgent,
        createdAt: sessions.createdAt,
        expiresAt: sessions.expiresAt,
      })
      .from(sessions)
      .leftJoin(users, eq(sessions.userId, users.id))
      .where(conditions)
      .orderBy(desc(sessions.createdAt))
      .limit(SESSIONS_PER_PAGE)
      .offset((page - 1) * SESSIONS_PER_PAGE),

    db
      .select({ count: count() })
      .from(sessions)
      .where(conditions)
      .then((res) => res[0]?.count || 0),
  ]);

  return {
    sessions: sessionList,
    totalCount,
    totalPages: Math.ceil(totalCount / SESSIONS_PER_PAGE),
    currentPage: page,
  };
}

export default async function SessionsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const session = await auth.api.getSession({
    headers: await import("next/headers").then((mod) => mod.headers()),
  });

  if (!session?.user) {
    redirect("/login");
  }

  if (session.user.role !== "admin") {
    redirect("/");
  }

  const {
    sessions: sessionList,
    totalCount,
    totalPages,
    currentPage,
  } = await getSessions(searchParams);

  const activeSessions = sessionList.filter(
    (s) => new Date(s.expiresAt) > new Date()
  ).length;

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="mb-8 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold mb-2">Session Management</h1>
          <p className="text-muted-foreground">
            {totalCount} total sessions ({activeSessions} active)
          </p>
        </div>
        <Link
          href="/admin"
          className="px-4 py-2 text-sm border rounded-lg hover:bg-accent"
        >
          ← Back to Dashboard
        </Link>
      </div>

      <div className="mb-6 bg-card rounded-lg border p-4">
        <form className="flex gap-4">
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              name="activeOnly"
              value="true"
              defaultChecked={searchParams.activeOnly === "true"}
              className="rounded border-gray-300 text-primary focus:ring-primary"
            />
            <span className="text-sm">Show active sessions only</span>
          </label>
          <button
            type="submit"
            className="px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90"
          >
            Apply Filter
          </button>
          <Link
            href="/admin/sessions"
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
                  IP Address
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  User Agent
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Created
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Expires
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Status
                </th>
                <th className="px-6 py-3 text-right text-xs font-medium uppercase tracking-wider">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {sessionList.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-6 py-8 text-center text-muted-foreground">
                    No sessions found
                  </td>
                </tr>
              ) : (
                sessionList.map((sess) => {
                  const isActive = new Date(sess.expiresAt) > new Date();
                  return (
                    <tr key={sess.id} className="hover:bg-muted/50">
                      <td className="px-6 py-4">
                        <div>
                          <p className="font-medium">
                            {sess.userName || "Unknown"}
                          </p>
                          <p className="text-sm text-muted-foreground">
                            {sess.userEmail || "No email"}
                          </p>
                        </div>
                      </td>
                      <td className="px-6 py-4 text-sm">
                        {sess.ipAddress || "Unknown"}
                      </td>
                      <td className="px-6 py-4 text-sm max-w-xs truncate">
                        {sess.userAgent || "Unknown"}
                      </td>
                      <td className="px-6 py-4 text-sm text-muted-foreground">
                        {new Date(sess.createdAt).toLocaleString()}
                      </td>
                      <td className="px-6 py-4 text-sm text-muted-foreground">
                        {new Date(sess.expiresAt).toLocaleString()}
                      </td>
                      <td className="px-6 py-4">
                        <span
                          className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                            isActive
                              ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
                              : "bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200"
                          }`}
                        >
                          {isActive ? "Active" : "Expired"}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-right">
                        {sess.userId && (
                          <Link
                            href={`/admin/users/${sess.userId}`}
                            className="text-primary hover:underline text-sm"
                          >
                            View User →
                          </Link>
                        )}
                      </td>
                    </tr>
                  );
                })
              )}
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
                href={`/admin/sessions?${new URLSearchParams({
                  ...searchParams,
                  page: (currentPage - 1).toString(),
                }).toString()}`}
                className="px-4 py-2 border rounded-lg hover:bg-accent"
              >
                ← Previous
              </Link>
            )}
            {currentPage < totalPages && (
              <Link
                href={`/admin/sessions?${new URLSearchParams({
                  ...searchParams,
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
