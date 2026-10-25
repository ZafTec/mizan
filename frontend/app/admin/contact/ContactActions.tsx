"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import {
  markContactHandled,
  replyToContact,
  type ContactSubmission,
} from "@/lib/api/admin-ops";
import { fieldErrors, replySchema } from "@/lib/admin-ops/schema";
import { appToast } from "@/lib/toast";

/**
 * Reply, or mark handled without one. A message gets one reply; after it the
 * form is gone and the drawer links to the reply in the mail log.
 */
export default function ContactActions({
  submission,
}: {
  submission: ContactSubmission;
}) {
  const router = useRouter();
  const [body, setBody] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const reply = async () => {
    const parsed = replySchema.safeParse({ body });
    if (!parsed.success) {
      setError(fieldErrors(parsed.error).body ?? "Write a reply");
      return;
    }
    setError(null);
    setBusy(true);
    try {
      await replyToContact(submission.id, parsed.data.body);
      appToast.success(`Reply queued to ${submission.email}`);
      setBody("");
      router.refresh();
    } catch (failure) {
      appToast.error(failure, "Could not send the reply");
    } finally {
      setBusy(false);
    }
  };

  const markHandled = async () => {
    setBusy(true);
    try {
      await markContactHandled(submission.id);
      appToast.success("Marked handled");
      router.refresh();
    } catch (failure) {
      appToast.error(failure, "Could not mark it handled");
    } finally {
      setBusy(false);
    }
  };

  if (submission.replyEmailMessageId) return null;

  return (
    <form
      className="mt-6 space-y-3"
      noValidate
      onSubmit={(e) => {
        e.preventDefault();
        void reply();
      }}
    >
      <div>
        <label className="label" htmlFor="contact-reply">
          Reply to {submission.name}
        </label>
        <textarea
          id="contact-reply"
          rows={6}
          className={`input ${error ? "input-error" : ""}`}
          value={body}
          onChange={(e) => setBody(e.target.value)}
          aria-invalid={Boolean(error)}
          aria-describedby={
            error ? "contact-reply-error" : "contact-reply-hint"
          }
        />
        {error ? (
          <p
            id="contact-reply-error"
            className="mt-1 text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
          >
            {error}
          </p>
        ) : (
          <p
            id="contact-reply-hint"
            className="mt-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400"
          >
            Sent from the site&apos;s address to {submission.email}. Replying
            marks the message handled.
          </p>
        )}
      </div>
      <div className="flex flex-wrap justify-end gap-2">
        {submission.status !== "Handled" && (
          <button
            type="button"
            className="btn-ghost"
            disabled={busy}
            onClick={markHandled}
          >
            Mark handled
          </button>
        )}
        <button type="submit" className="btn-primary" disabled={busy}>
          {busy ? "Sending…" : "Send reply"}
        </button>
      </div>
    </form>
  );
}
