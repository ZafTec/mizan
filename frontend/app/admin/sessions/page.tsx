import Link from "next/link";
import { redirect } from "next/navigation";
import { getCurrentUser } from "@/lib/auth";
import { listAdminSessions, type AdminSession } from "@/data/admin/users";
import {
  DataTable,
  Pill,
  TableToolbar,
  readPage,
  type Column,
} from "@/components/ui/data-table";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export const metadata = {
  title: "Sessions | Admin",
  description: "Active sign-ins across the system",
};

const PAGE_SIZE = 50;

function relative(iso: string): string {
  const minutes = Math.round((Date.now() - Date.parse(iso)) / 60000);
  if (!Number.isFinite(minutes)) return "—";
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.round(hours / 24)}d ago`;
}

/** A user agent is unreadable at table width; the family is the useful part. */
function device(agent?: string | null): string {
  if (!agent) return "Unknown";
  if (/iPhone|iPad|Android/i.test(agent)) return "Mobile";
  if (/Edg\//i.test(agent)) return "Edge";
  if (/Chrome\//i.test(agent)) return "Chrome";
  if (/Safari\//i.test(agent)) return "Safari";
  if (/Firefox\//i.test(agent)) return "Firefox";
  return "Other";
}

const columns: Column<AdminSession>[] = [
  {
    id: "user",
    header: "User",
    cell: (s) => (
      <div className="min-w-0">
        <p className="truncate font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
          {s.userName || "Unnamed"}
        </p>
        <p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
          {s.userEmail}
        </p>
      </div>
    ),
  },
  {
    id: "status",
    header: "Status",
    cell: (s) =>
      Date.parse(s.expiresAt) > Date.now() ? (
        <Pill tone="good">Active</Pill>
      ) : (
        <Pill>Expired</Pill>
      ),
  },
  {
    id: "device",
    header: "Device",
    secondary: true,
    cell: (s) => device(s.userAgent),
  },
  {
    id: "ip",
    header: "IP",
    secondary: true,
    cell: (s) => (
      <span className="font-mono text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
        {s.ipAddress || "—"}
      </span>
    ),
  },
  {
    id: "seen",
    header: "Last seen",
    cell: (s) => (
      <span className="whitespace-nowrap text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
        {relative(s.lastSeenAt)}
      </span>
    ),
  },
  {
    id: "expires",
    header: "Expires",
    secondary: true,
    cell: (s) => (
      <span className="whitespace-nowrap text-xs tabular-nums text-charcoal-blue-500 dark:text-charcoal-blue-400">
        {new Date(s.expiresAt).toLocaleDateString()}
      </span>
    ),
  },
  {
    id: "actions",
    header: "",
    align: "right",
    width: "1%",
    cell: (s) => (
      <Link
        href={`/admin/users/${s.userId}`}
        className="text-link whitespace-nowrap"
      >
        User
      </Link>
    ),
  },
];

export default async function AdminSessionsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const params = await searchParams;
  const user = await getCurrentUser();
  if (!user) redirect("/login");
  if (user.role !== "admin") redirect("/");

  const page = readPage(params);
  const result = await listAdminSessions({
    page,
    pageSize: PAGE_SIZE,
    activeOnly: params.activeOnly === "true",
  });

  return (
    <div className="flex flex-col gap-6 [&>header]:mb-2">
      <AdminPageHeader
        title="Sessions"
        lead="Every sign-in the system is holding; revoking one signs that device out on its next request."
      />

      <TableToolbar
        filters={[
          {
            name: "activeOnly",
            label: "All sessions",
            options: [{ value: "true", label: "Active only" }],
          },
        ]}
      />

      <DataTable
        columns={columns}
        rows={result.items}
        rowKey={(s) => s.id}
        pathname="/admin/sessions"
        searchParams={params}
        page={{
          page: result.page,
          pageSize: result.pageSize,
          totalCount: result.totalCount,
          totalPages: result.totalPages,
        }}
        empty={{
          title: "No sessions",
          description: "Nobody is signed in right now.",
        }}
      />
    </div>
  );
}
