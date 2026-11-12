import Link from "next/link";
import { listAiPrompts, getGlobalAiUsage } from "@/data/admin/ai";
import AdminPageHeader from "@/components/admin/AdminPageHeader";
import { Pill } from "@/components/ui/data-table";
import { Icon } from "@/components/ui/icon";

export const metadata = {
  title: "Assistant | Mizan admin",
  description: "Prompt versions, evals and spend",
};

function currency(micros: number) {
  return `$${(micros / 1_000_000).toFixed(2)}`;
}

function Meter({ used, ceiling }: { used: number; ceiling: number }) {
  const pct = ceiling > 0 ? Math.min(100, (used / ceiling) * 100) : 0;
  return (
    <div className="target-track w-full">
      <span
        className={pct > 85 ? "bg-destructive" : "bg-chart-1"}
        style={{ width: `${pct}%` }}
      />
    </div>
  );
}

export default async function AdminAiPage() {
  const [prompts, usage] = await Promise.all([
    listAiPrompts(),
    getGlobalAiUsage(),
  ]);

  return (
    <div className="flex flex-col gap-10 [&>header]:mb-0">
      <AdminPageHeader
        title="Assistant"
        lead="Edit a prompt, prove it against the adversarial suite, then publish it."
      />

      <section aria-labelledby="today-title" className="flex flex-col">
        <div className="log-section-header flex-wrap">
          <h2 id="today-title" className="text-[20px] font-semibold">
            Today
          </h2>
          <span className="text-[12px] text-muted-foreground">
            {usage.requestsToday} calls · {usage.activeUsersToday} people ·{" "}
            {usage.failuresToday} failed
          </span>
        </div>

        <ul>
          <li className="flex flex-col gap-2 border-b border-border py-3">
            <div className="flex items-baseline justify-between gap-3 text-[14px]">
              <span className="text-muted-foreground">Tokens</span>
              <span className="tabular-nums">
                {usage.tokensToday.toLocaleString()} /{" "}
                {usage.tokenCeiling.toLocaleString()}
              </span>
            </div>
            <Meter used={usage.tokensToday} ceiling={usage.tokenCeiling} />
          </li>
          <li className="flex flex-col gap-2 border-b border-border py-3">
            <div className="flex items-baseline justify-between gap-3 text-[14px]">
              <span className="text-muted-foreground">Spend</span>
              <span className="tabular-nums">
                {currency(usage.costMicrosToday)} /{" "}
                {currency(usage.costCeilingMicros)}
              </span>
            </div>
            <Meter
              used={usage.costMicrosToday}
              ceiling={usage.costCeilingMicros}
            />
          </li>
          {usage.byFeature.map((f) => (
            <li
              key={f.feature}
              className="flex min-h-11 items-center justify-between gap-3 border-b border-border py-2 text-[14px]"
            >
              <span className="min-w-0 truncate">{f.feature}</span>
              <span className="text-muted-foreground tabular-nums">
                {f.tokens.toLocaleString()} tokens
              </span>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="prompts-title" className="flex flex-col">
        <div className="log-section-header">
          <h2 id="prompts-title" className="text-[20px] font-semibold">
            Prompts
          </h2>
        </div>

        {prompts.length === 0 ? (
          <p className="log-empty text-[14px] text-muted-foreground">
            No prompts yet.
          </p>
        ) : (
          <ul>
            {prompts.map((prompt) => (
              <li key={prompt.key}>
                <Link
                  href={`/admin/ai/${encodeURIComponent(prompt.key)}`}
                  className="ledger-row transition-colors duration-150 hover:bg-muted/60"
                >
                  <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                    <code className="text-[14px] font-semibold break-all">
                      {prompt.key}
                    </code>
                    <span className="text-[12px] text-muted-foreground">
                      {prompt.description}
                    </span>
                    <span className="text-[12px] text-muted-foreground">
                      {prompt.versionCount} version
                      {prompt.versionCount === 1 ? "" : "s"}
                      {prompt.draftCount > 0 && ` · ${prompt.draftCount} draft`}
                      {prompt.draftCount > 1 && "s"}
                    </span>
                  </span>
                  {prompt.publishedVersion === null ? (
                    <Pill tone="warn" className="shrink-0">
                      Built-in default
                    </Pill>
                  ) : (
                    <Pill tone="good" className="shrink-0">
                      v{prompt.publishedVersion} live
                    </Pill>
                  )}
                  <Icon
                    name="arrowRight"
                    size={16}
                    className="shrink-0 text-muted-foreground"
                  />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
