import { Skeleton } from "@/components/ui/skeleton";

/**
 * Loading states for the admin console. Each one keeps the header's spacing
 * so the page does not jump when it arrives.
 */
function Bar({ className }: { className: string }) {
  return <Skeleton className={`rounded-[3px] ${className}`} />;
}

function HeaderSkeleton({ back = false }: { back?: boolean }) {
  return (
    <div className="mb-8 flex flex-col gap-1.5 max-sm:mb-6" aria-hidden>
      {back ? <Bar className="h-4 w-20" /> : null}
      <Bar className="h-[39px] w-56 max-sm:h-[35px]" />
      <Bar className="h-4 w-full max-w-md" />
    </div>
  );
}

function Rows({ rows }: { rows: number }) {
  return (
    <div className="border-t border-foreground" aria-hidden>
      {Array.from({ length: rows }).map((_, i) => (
        <div
          key={i}
          className="flex min-h-14 items-center gap-4 border-b border-border py-3"
        >
          <Bar className="h-4 w-1/3" />
          <Bar className="ml-auto h-4 w-16" />
        </div>
      ))}
    </div>
  );
}

function Status() {
  return (
    <span role="status" className="sr-only">
      Loading
    </span>
  );
}

/** A list or table page: header, a filter line and ruled rows. */
export function AdminListSkeleton({ rows = 8 }: { rows?: number }) {
  return (
    <div>
      <Status />
      <HeaderSkeleton />
      <Bar className="mb-6 h-10 w-full max-w-sm" />
      <Rows rows={rows} />
    </div>
  );
}

/** A record's page: back link, facts strip and a ruled section. */
export function AdminDetailSkeleton() {
  return (
    <div>
      <Status />
      <HeaderSkeleton back />
      <div
        className="mb-10 grid grid-cols-3 border-y border-border"
        aria-hidden
      >
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="flex flex-col gap-2 py-4 pr-4">
            <Bar className="h-3 w-16" />
            <Bar className="h-6 w-12" />
          </div>
        ))}
      </div>
      <Rows rows={5} />
    </div>
  );
}

/** A create or edit page: back link and labelled fields. */
export function AdminFormSkeleton({ fields = 5 }: { fields?: number }) {
  return (
    <div className="max-w-2xl">
      <Status />
      <HeaderSkeleton back />
      <div className="flex flex-col gap-5" aria-hidden>
        {Array.from({ length: fields }).map((_, i) => (
          <div key={i} className="flex flex-col gap-2">
            <Bar className="h-4 w-24" />
            <Bar className="h-10 w-full" />
          </div>
        ))}
        <Bar className="h-10 w-32" />
      </div>
    </div>
  );
}

/** The overview: header, facts strip and two ruled sections. */
export function AdminOverviewSkeleton() {
  return (
    <div>
      <Status />
      <HeaderSkeleton />
      <div
        className="mb-10 grid grid-cols-2 border-y border-border sm:grid-cols-4"
        aria-hidden
      >
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="flex flex-col gap-2 py-4 pr-4">
            <Bar className="h-3 w-16" />
            <Bar className="h-6 w-12" />
          </div>
        ))}
      </div>
      <div className="grid gap-10 lg:grid-cols-2">
        <Rows rows={4} />
        <Rows rows={4} />
      </div>
    </div>
  );
}
