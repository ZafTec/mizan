import type { CampaignPreview } from "@/lib/api/admin-notifications";
import { CHANNELS } from "@/lib/admin-notifications/schema";

/** Each channel's rendering of the message, as the API returns it, with the warnings to fix first. */
export default function ChannelPreviews({
  preview,
}: {
  preview: CampaignPreview;
}) {
  return (
    <section aria-labelledby="campaign-preview" className="space-y-3">
      <h2
        id="campaign-preview"
        className="text-sm font-semibold uppercase tracking-wide text-muted-foreground"
      >
        Preview
      </h2>
      {preview.warnings.length > 0 && (
        <ul
          role="alert"
          className="space-y-1 text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
        >
          {preview.warnings.map((w) => (
            <li key={w}>{w}</li>
          ))}
        </ul>
      )}
      {preview.channels.map((c) => (
        <article
          key={c.channel}
          className="border border-charcoal-blue-200 p-3 text-sm dark:border-charcoal-blue-700"
          data-testid={`preview-${c.channel}`}
        >
          <p className="text-xs uppercase tracking-wide text-muted-foreground">
            {CHANNELS.find((x) => x.value === c.channel)?.label ?? c.channel}
          </p>
          <p className="mt-1 font-medium break-words">{c.title}</p>
          {c.body && (
            <p className="mt-1 whitespace-pre-line break-words">{c.body}</p>
          )}
          {c.imageUrl && (
            <p className="mt-1 truncate text-muted-foreground">
              Image: {c.imageUrl}
            </p>
          )}
          {c.buttons.length > 0 && (
            <ul className="mt-2 flex flex-wrap gap-2">
              {c.buttons.map((b) => (
                <li
                  key={b}
                  className="border border-charcoal-blue-200 px-2 py-1 text-xs break-all dark:border-charcoal-blue-700"
                >
                  {b}
                </li>
              ))}
            </ul>
          )}
          {c.footer && (
            <p className="mt-2 text-xs text-muted-foreground break-words">
              {c.footer}
            </p>
          )}
        </article>
      ))}
    </section>
  );
}
