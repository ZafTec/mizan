import Link from "next/link";
import { redirect } from "next/navigation";
import { getCurrentUser } from "@/lib/auth";
import AdminPageHeader from "@/components/admin/AdminPageHeader";
import { listAdminUsers, type AdminUser } from "@/data/admin/users";
import {
  DataTable,
  Pill,
  TableToolbar,
  readPage,
  readSort,
  type Column,
} from "@/components/ui/data-table";

export const metadata = {
  title: "Users | Admin",
  description: "Accounts, roles and bans",
};

const PAGE_SIZE = 20;

const ROLE_TONE = { admin: "bad", trainer: "info" } as const;

function date(value?: string | null) {
  return value ? new Date(value).toLocaleDateString() : "—";
}

const columns: Column<AdminUser>[] = [
  {
    id: "user",
    header: "User",
    sortKey: "name",
    cell: (u) => (
      <div className="min-w-0">
        <p className="truncate font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
          {u.name || "Unnamed"}
        </p>
        <p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
          {u.email}
        </p>
      </div>
    ),
  },
  {
    id: "role",
    header: "Role",
    sortKey: "role",
    cell: (u) => (
      <Pill tone={ROLE_TONE[u.role as keyof typeof ROLE_TONE] ?? "neutral"}>
        {u.role}
      </Pill>
    ),
  },
  {
    id: "status",
    header: "Status",
    cell: (u) =>
      u.banned ? (
        <Pill tone="bad">Suspended</Pill>
      ) : (
        <Pill tone="good">Active</Pill>
      ),
  },
  {
    id: "verified",
    header: "Verified",
    secondary: true,
    cell: (u) =>
      u.emailVerified ? <Pill tone="good">Yes</Pill> : <Pill>No</Pill>,
  },
  {
    id: "joined",
    header: "Joined",
    sortKey: "createdAt",
    secondary: true,
    cell: (u) => (
      <span className="text-xs tabular-nums text-charcoal-blue-500 dark:text-charcoal-blue-400">
        {date(u.createdAt)}
      </span>
    ),
  },
  {
    id: "actions",
    header: "",
    align: "right",
    width: "1%",
    cell: (u) => (
      <Link
        href={`/admin/users/${u.id}`}
        className="text-link whitespace-nowrap"
      >
        Details
      </Link>
    ),
  },
];

export default async function AdminUsersPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const params = await searchParams;
  const user = await getCurrentUser();
  if (!user) redirect("/login");
  if (user.role !== "admin") redirect("/");

  const page = readPage(params);
  const sort = readSort(params);

  const result = await listAdminUsers({
    page,
    pageSize: PAGE_SIZE,
    search: params.search || undefined,
    role: params.role || undefined,
    banned: params.banned === "true" ? true : undefined,
    sortBy: sort.sortBy ?? undefined,
    sortOrder: sort.sortOrder,
  });

  return (
    <div className="flex flex-col gap-6 [&>header]:mb-2">
      <AdminPageHeader
        title="Users"
        lead="Accounts, their roles and suspensions."
        action={
          <Link href="/admin/users/create" className="btn-primary">
            New user
          </Link>
        }
      />

      <TableToolbar
        searchPlaceholder="Name or email…"
        filters={[
          {
            name: "role",
            label: "Any role",
            options: [
              { value: "user", label: "User" },
              { value: "trainer", label: "Trainer" },
              { value: "admin", label: "Admin" },
            ],
          },
          {
            name: "banned",
            label: "Any status",
            options: [{ value: "true", label: "Suspended only" }],
          },
        ]}
      />

      <DataTable
        columns={columns}
        rows={result.items}
        rowKey={(u) => u.id}
        pathname="/admin/users"
        searchParams={params}
        sort={sort}
        page={{
          page: result.page,
          pageSize: result.pageSize,
          totalCount: result.totalCount,
          totalPages: result.totalPages,
        }}
        empty={{
          title: "No users match",
          description: "Try a different search or clear the filters.",
        }}
      />
    </div>
  );
}
