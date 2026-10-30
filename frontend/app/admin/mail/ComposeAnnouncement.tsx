"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  previewAnnouncementAudience,
  searchHouseholds,
  searchUsers,
  sendAnnouncement,
  type AnnouncementAudience,
} from "@/lib/api/admin-ops";
import {
  announcementSchema,
  audienceOf,
  fieldErrors,
  type AudienceKind,
} from "@/lib/admin-ops/schema";
import { appToast, getErrorMessage } from "@/lib/toast";

const AUDIENCES: { kind: AudienceKind; label: string }[] = [
  { kind: "all", label: "Everyone" },
  { kind: "pro", label: "Pro subscribers" },
  { kind: "free", label: "Free accounts" },
  { kind: "household", label: "One household" },
  { kind: "user", label: "One person" },
];

type Target = { id: string; label: string; detail?: string | null };

/**
 * Compose, then confirm. The confirmation step asks the API who the audience
 * reaches and shows that count before anything is queued; the announcement id
 * is made there, so a second press or a retry sends nobody a second copy.
 */
export default function ComposeAnnouncement() {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [kind, setKind] = useState<AudienceKind>("all");
  const [target, setTarget] = useState<Target | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [preview, setPreview] = useState<AnnouncementAudience | null>(null);
  const [announcementId, setAnnouncementId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const reset = () => {
    setSubject("");
    setBody("");
    setKind("all");
    setTarget(null);
    setErrors({});
    setPreview(null);
    setAnnouncementId(null);
  };

  const review = async () => {
    const parsed = announcementSchema.safeParse({
      subject,
      body,
      kind,
      targetId: target?.id ?? null,
    });
    if (!parsed.success) {
      setErrors(fieldErrors(parsed.error));
      return;
    }
    setErrors({});
    setBusy(true);
    try {
      setPreview(await previewAnnouncementAudience(audienceOf(parsed.data)));
      setAnnouncementId(crypto.randomUUID());
    } catch (error) {
      setErrors({
        form: getErrorMessage(error, "Could not count the audience"),
      });
    } finally {
      setBusy(false);
    }
  };

  const send = async () => {
    if (!preview || !announcementId) return;
    setBusy(true);
    try {
      const result = await sendAnnouncement({
        subject: subject.trim(),
        body,
        audience: preview.audience,
        announcementId,
      });
      appToast.success(
        `Queued ${result.queued} ${result.queued === 1 ? "email" : "emails"}`,
      );
      setOpen(false);
      reset();
      router.refresh();
    } catch (error) {
      setErrors({ form: getErrorMessage(error, "Could not send it") });
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <button
        type="button"
        className="btn-primary"
        onClick={() => setOpen(true)}
      >
        Compose announcement
      </button>
      <Dialog
        open={open}
        onOpenChange={(next) => {
          setOpen(next);
          if (!next) reset();
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {preview ? "Send this announcement?" : "Compose announcement"}
            </DialogTitle>
            <DialogDescription>
              {preview
                ? "Check who receives it. Nothing is queued until you send."
                : "Plain text. Blank lines start a new paragraph."}
            </DialogDescription>
          </DialogHeader>

          {preview ? (
            <>
              <DialogBody className="space-y-4">
                <p
                  role="status"
                  className="border border-charcoal-blue-200 p-3 text-sm dark:border-charcoal-blue-700"
                >
                  <span className="font-medium">“{subject.trim()}”</span> goes
                  to{" "}
                  <strong className="tabular-nums">
                    {preview.recipientCount}{" "}
                    {preview.recipientCount === 1 ? "recipient" : "recipients"}
                  </strong>{" "}
                  ({preview.label}).
                </p>
                {preview.recipientCount === 0 && (
                  <p className="text-sm text-burnt-peach-700 dark:text-burnt-peach-400">
                    Nobody in that audience can receive email.
                  </p>
                )}
                {errors.form && (
                  <p
                    role="alert"
                    className="text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
                  >
                    {errors.form}
                  </p>
                )}
              </DialogBody>
              <DialogFooter>
                <button
                  type="button"
                  className="btn-ghost"
                  disabled={busy}
                  onClick={() => {
                    setPreview(null);
                    setAnnouncementId(null);
                    setErrors({});
                  }}
                >
                  Back
                </button>
                <button
                  type="button"
                  className="btn-primary"
                  disabled={busy || preview.recipientCount === 0}
                  onClick={send}
                >
                  {busy ? "Sending…" : `Send to ${preview.recipientCount}`}
                </button>
              </DialogFooter>
            </>
          ) : (
            <form
              className="flex min-h-0 flex-1 flex-col gap-4"
              noValidate
              onSubmit={(e) => {
                e.preventDefault();
                void review();
              }}
            >
              <DialogBody className="space-y-4">
                <div>
                  <label className="label" htmlFor="announcement-subject">
                    Subject
                  </label>
                  <input
                    id="announcement-subject"
                    className={`input ${errors.subject ? "input-error" : ""}`}
                    value={subject}
                    maxLength={200}
                    onChange={(e) => setSubject(e.target.value)}
                    aria-invalid={Boolean(errors.subject)}
                    aria-describedby={
                      errors.subject ? "announcement-subject-error" : undefined
                    }
                  />
                  {errors.subject && (
                    <p
                      id="announcement-subject-error"
                      className="mt-1 text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
                    >
                      {errors.subject}
                    </p>
                  )}
                </div>
                <div>
                  <label className="label" htmlFor="announcement-body">
                    Message
                  </label>
                  <textarea
                    id="announcement-body"
                    rows={6}
                    className={`input ${errors.body ? "input-error" : ""}`}
                    value={body}
                    onChange={(e) => setBody(e.target.value)}
                    aria-invalid={Boolean(errors.body)}
                    aria-describedby={
                      errors.body ? "announcement-body-error" : undefined
                    }
                  />
                  {errors.body && (
                    <p
                      id="announcement-body-error"
                      className="mt-1 text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
                    >
                      {errors.body}
                    </p>
                  )}
                </div>
                <fieldset>
                  <legend className="label">Audience</legend>
                  <div className="grid grid-cols-1 gap-1 sm:grid-cols-2">
                    {AUDIENCES.map((option) => (
                      <label
                        key={option.kind}
                        className="flex min-h-10 items-center gap-2 text-sm"
                      >
                        <input
                          type="radio"
                          name="audience"
                          value={option.kind}
                          checked={kind === option.kind}
                          onChange={() => {
                            setKind(option.kind);
                            setTarget(null);
                          }}
                        />
                        {option.label}
                      </label>
                    ))}
                  </div>
                </fieldset>
                {(kind === "household" || kind === "user") && (
                  <TargetPicker
                    key={kind}
                    kind={kind}
                    value={target}
                    onChange={setTarget}
                    error={errors.targetId}
                  />
                )}
                {errors.form && (
                  <p
                    role="alert"
                    className="text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
                  >
                    {errors.form}
                  </p>
                )}
              </DialogBody>
              <DialogFooter>
                <button type="submit" className="btn-primary" disabled={busy}>
                  {busy ? "Counting…" : "Review"}
                </button>
              </DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}

function TargetPicker({
  kind,
  value,
  onChange,
  error,
}: {
  kind: "household" | "user";
  value: Target | null;
  onChange: (target: Target | null) => void;
  error?: string;
}) {
  const [term, setTerm] = useState("");
  const [results, setResults] = useState<Target[]>([]);
  const label = kind === "household" ? "Find a household" : "Find a person";
  // A short term shows nothing, whatever an earlier search found.
  const shown = term.trim().length < 2 ? [] : results;

  useEffect(() => {
    const query = term.trim();
    if (query.length < 2) return;
    let cancelled = false;
    const timer = setTimeout(async () => {
      try {
        const found =
          kind === "household"
            ? (await searchHouseholds(query)).items.map((h) => ({
                id: h.id,
                label: h.name,
                detail: `${h.memberCount} ${h.memberCount === 1 ? "member" : "members"}`,
              }))
            : (await searchUsers(query)).items.map((u) => ({
                id: u.id,
                label: u.name || u.email,
                detail: u.email,
              }));
        if (!cancelled) setResults(found);
      } catch {
        if (!cancelled) setResults([]);
      }
    }, 250);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [term, kind]);

  if (value)
    return (
      <div className="flex items-center justify-between gap-3 border border-charcoal-blue-200 p-3 text-sm dark:border-charcoal-blue-700">
        <span className="min-w-0">
          <span className="block truncate font-medium">{value.label}</span>
          {value.detail && (
            <span className="block truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
              {value.detail}
            </span>
          )}
        </span>
        <button
          type="button"
          className="btn-ghost btn-sm"
          onClick={() => onChange(null)}
        >
          Change
        </button>
      </div>
    );

  return (
    <div>
      <label className="label" htmlFor="announcement-target">
        {label}
      </label>
      <input
        id="announcement-target"
        type="search"
        className={`input ${error ? "input-error" : ""}`}
        placeholder={
          kind === "household" ? "Name or member email" : "Name or email"
        }
        value={term}
        onChange={(e) => setTerm(e.target.value)}
        aria-invalid={Boolean(error)}
      />
      {error && (
        <p className="mt-1 text-sm text-burnt-peach-700 dark:text-burnt-peach-400">
          {error}
        </p>
      )}
      {shown.length > 0 && (
        <ul
          aria-label={`${label}: results`}
          className="mt-2 divide-y divide-charcoal-blue-100 border border-charcoal-blue-200 dark:divide-white/10 dark:border-charcoal-blue-700"
        >
          {shown.map((result) => (
            <li key={result.id}>
              <button
                type="button"
                className="flex w-full min-h-10 flex-col items-start px-3 py-2 text-left text-sm hover:bg-charcoal-blue-50 focus:bg-charcoal-blue-50 focus:outline-none dark:hover:bg-white/5 dark:focus:bg-white/5"
                onClick={() => onChange(result)}
              >
                <span className="font-medium">{result.label}</span>
                {result.detail && (
                  <span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                    {result.detail}
                  </span>
                )}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
