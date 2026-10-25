import {
  DataTable,
  Pill,
  TableToolbar,
  readPage,
  withParams,
  type Column,
} from "@/components/ui/data-table";
import AdminDrawer, { DrawerField } from "@/components/admin/AdminDrawer";
import { CONTACT_STATUSES, type ContactSubmission } from "@/lib/api/admin-ops";
import {
  getContactServer,
  listContactServer,
} from "@/lib/api/admin-ops.server";
import ContactActions from "./ContactActions";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Contact | Mizan admin",
  description: "Messages sent through the contact form",
};

const PAGE_SIZE = 20;

function when(value?: string | null) {
  if (!value) return "-";
  return new Date(value).toLocaleString("en-GB", {
    day: "numeric",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "UTC",
  });
}

/**
 * /admin/contact: the contact form's inbox (docs/ADMIN.md#contact). A row
 * opens the message with a reply form; replying emails the sender from the
 * site's address and marks the message handled.
 */
export default async function AdminContactPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const params = await searchParams;
  const page = readPage(params);
  const [list, open] = await Promise.all([
    listContactServer({
      Status: params.status,
      Search: params.search,
      Page: page,
      PageSize: PAGE_SIZE,
    }),
    params.open ? getContactServer(params.open) : Promise.resolve(null),
  ]);

  const columns: Column<ContactSubmission>[] = [
    {
      id: "subject",
      header: "Message",
      cell: (c) => (
        <span className="block min-w-0">
          <span className="block truncate font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
            {c.subject}
          </span>
          <span className="block truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
            {c.name} · {c.email}
          </span>
        </span>
      ),
    },
    {
      id: "status",
      header: "Status",
      cell: (c) => (
        <Pill tone={c.status === "New" ? "info" : "good"}>
          {c.status === "New"
            ? "New"
            : c.replyEmailMessageId
              ? "Replied"
              : "Handled"}
        </Pill>
      ),
    },
    {
      id: "created",
      header: "Received (UTC)",
      secondary: true,
      cell: (c) => (
        <span className="whitespace-nowrap text-xs tabular-nums text-charcoal-blue-500 dark:text-charcoal-blue-400">
          {when(c.createdAt)}
        </span>
      ),
    },
  ];

  return (
    <div className="space-y-6 lg:space-y-8">
      <header className="space-y-2">
        <p className="eyebrow">Administration</p>
        <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
          Contact
        </h1>
        <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
          What people sent through the contact form.
        </p>
      </header>

      <TableToolbar
        searchPlaceholder="Search messages…"
        filters={[
          {
            name: "status",
            label: "Any status",
            options: CONTACT_STATUSES.map((s) => ({ value: s, label: s })),
          },
        ]}
      />

      <DataTable
        columns={columns}
        rows={list.items}
        rowKey={(c) => c.id}
        pathname="/admin/contact"
        searchParams={params}
        sort={{ sortBy: null, sortOrder: "desc" }}
        page={{
          page,
          pageSize: PAGE_SIZE,
          totalCount: list.totalCount,
          totalPages: list.totalPages,
        }}
        href={(c) => withParams("/admin/contact", params, { open: c.id })}
        empty={{
          title: "No messages",
          description: "Nothing matches, or nobody has written yet.",
        }}
      />

      {open && (
        <AdminDrawer
          title={open.subject}
          description={`From ${open.name} <${open.email}>`}
          closeHref={withParams("/admin/contact", params, { open: null })}
        >
          <dl>
            <DrawerField label="Received">
              {when(open.createdAt)} UTC
            </DrawerField>
            <DrawerField label="Account">
              {open.userId ? (
                <a className="underline" href={`/admin/users/${open.userId}`}>
                  Signed in
                </a>
              ) : (
                "Not signed in"
              )}
            </DrawerField>
            <DrawerField label="Status">
              {open.status === "Handled"
                ? `Handled${open.handledByName ? ` by ${open.handledByName}` : ""}${open.handledAt ? `, ${when(open.handledAt)} UTC` : ""}`
                : "New"}
            </DrawerField>
            {open.replyStatus && (
              <DrawerField label="Reply">
                <a
                  className="underline"
                  href={`/admin/mail?open=${open.replyEmailMessageId}`}
                >
                  {open.replyStatus}
                </a>
              </DrawerField>
            )}
          </dl>
          <section className="mt-4" aria-labelledby="contact-message">
            <h3
              id="contact-message"
              className="mb-1 text-sm font-medium text-charcoal-blue-700 dark:text-charcoal-blue-300"
            >
              Message
            </h3>
            <p className="whitespace-pre-wrap break-words border border-charcoal-blue-200 p-3 text-sm dark:border-charcoal-blue-700">
              {open.message}
            </p>
          </section>
          <ContactActions submission={open} />
        </AdminDrawer>
      )}
    </div>
  );
}
