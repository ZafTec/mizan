import Link from "next/link";

/**
 * The head of every admin page: an optional link back up, the title, one line
 * of lead and at most one filled action. The console's sidebar names the
 * section, so pages carry no eyebrow.
 */
export default function AdminPageHeader({
  title,
  lead,
  back,
  action,
}: {
  title: React.ReactNode;
  lead?: React.ReactNode;
  back?: { href: string; label: string };
  action?: React.ReactNode;
}) {
  return (
    <header className="mb-8 flex flex-col gap-4 max-sm:mb-6 sm:flex-row sm:items-end sm:justify-between">
      <div className="flex min-w-0 flex-col gap-1.5">
        {back ? (
          <Link href={back.href} className="text-link w-fit text-[13px]">
            {back.label}
          </Link>
        ) : null}
        <h1 className="text-[34px] leading-[1.15] font-semibold tracking-[-0.022em] break-words max-sm:text-[30px]">
          {title}
        </h1>
        {lead ? (
          <p className="max-w-[68ch] text-[14px] text-muted-foreground">
            {lead}
          </p>
        ) : null}
      </div>
      {action ? <div className="flex shrink-0 gap-2">{action}</div> : null}
    </header>
  );
}
