import { cn } from "@/lib/utils";

export type AdminFact = {
  label: string;
  value: React.ReactNode;
  note?: React.ReactNode;
};

/**
 * Up to four figures in the ruled strip the overview uses: an ink rule above,
 * a hairline below and a hairline between figures. Four figures fold to two
 * columns on a phone.
 */
export default function AdminFacts({
  facts,
  className,
}: {
  facts: AdminFact[];
  className?: string;
}) {
  return (
    <dl
      className={cn(
        "grid border-t border-b border-t-foreground border-b-border",
        facts.length >= 4
          ? "grid-cols-2 sm:grid-cols-4"
          : facts.length === 3
            ? "grid-cols-3"
            : "grid-cols-2",
        className,
      )}
    >
      {facts.map((fact) => (
        <div
          key={fact.label}
          className={cn(
            "flex min-w-0 flex-col gap-0.5 border-r border-border py-3.5 pr-4 pl-4 first:pl-0 last:border-r-0",
            facts.length >= 4 &&
              "max-sm:nth-2:border-r-0 max-sm:nth-3:pl-0 max-sm:nth-[n+3]:border-t",
          )}
        >
          <dt className="text-[12px] text-muted-foreground">{fact.label}</dt>
          <dd className="text-[24px] leading-tight font-semibold break-words tabular-nums max-sm:text-[20px]">
            {fact.value}
          </dd>
          {fact.note ? (
            <dd className="text-[12px] text-muted-foreground">{fact.note}</dd>
          ) : null}
        </div>
      ))}
    </dl>
  );
}
