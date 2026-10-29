"use client";

import { useState } from "react";
import {
  addSupportNote,
  deleteSupportNote,
  listSupportNotes,
  updateSupportNote,
  type SupportNote,
  type SupportNoteSubject,
} from "@/lib/api/admin-ops";
import { fieldErrors, supportNoteSchema } from "@/lib/admin-ops/schema";
import { appToast } from "@/lib/toast";

function when(value: string) {
  return new Date(value).toLocaleString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "UTC",
  });
}

/**
 * Administrators' notes on a user or a household, newest first. Only
 * administrators reach this page or the API behind it; the subject never
 * sees a note. Any administrator may edit or delete one.
 */
export default function SupportNotesPanel({
  subjectType,
  subjectId,
  initialNotes,
}: {
  subjectType: SupportNoteSubject;
  subjectId: string;
  initialNotes: SupportNote[];
}) {
  const [notes, setNotes] = useState(initialNotes);
  const [draft, setDraft] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<{ id: string; body: string } | null>(
    null,
  );
  const [busy, setBusy] = useState(false);

  const reload = async () =>
    setNotes(await listSupportNotes(subjectType, subjectId));

  const add = async () => {
    const parsed = supportNoteSchema.safeParse({ body: draft });
    if (!parsed.success) {
      setError(fieldErrors(parsed.error).body ?? "Write a note");
      return;
    }
    setError(null);
    setBusy(true);
    try {
      await addSupportNote(subjectType, subjectId, parsed.data.body);
      setDraft("");
      await reload();
      appToast.success("Note added");
    } catch (failure) {
      appToast.error(failure, "Could not add the note");
    } finally {
      setBusy(false);
    }
  };

  const save = async () => {
    if (!editing) return;
    const parsed = supportNoteSchema.safeParse({ body: editing.body });
    if (!parsed.success) {
      appToast.error(fieldErrors(parsed.error).body ?? "Write a note");
      return;
    }
    setBusy(true);
    try {
      await updateSupportNote(editing.id, parsed.data.body);
      setEditing(null);
      await reload();
      appToast.success("Note saved");
    } catch (failure) {
      appToast.error(failure, "Could not save the note");
    } finally {
      setBusy(false);
    }
  };

  const remove = async (note: SupportNote) => {
    if (!window.confirm("Delete this note for every administrator?")) return;
    setBusy(true);
    try {
      await deleteSupportNote(note.id);
      await reload();
      appToast.success("Note deleted");
    } catch (failure) {
      appToast.error(failure, "Could not delete the note");
    } finally {
      setBusy(false);
    }
  };

  return (
    <section
      aria-labelledby="support-notes-title"
      className="border border-charcoal-blue-200 p-5 sm:p-6 dark:border-charcoal-blue-700"
    >
      <h2
        id="support-notes-title"
        className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50"
      >
        Support notes
      </h2>
      <p className="mb-4 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
        Seen by administrators only.
      </p>

      <form
        className="space-y-2"
        noValidate
        onSubmit={(e) => {
          e.preventDefault();
          void add();
        }}
      >
        <label className="label" htmlFor="support-note-new">
          New note
        </label>
        <textarea
          id="support-note-new"
          rows={3}
          className={`input ${error ? "input-error" : ""}`}
          value={draft}
          maxLength={4000}
          onChange={(e) => setDraft(e.target.value)}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? "support-note-new-error" : undefined}
        />
        {error && (
          <p
            id="support-note-new-error"
            className="text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
          >
            {error}
          </p>
        )}
        <div className="flex justify-end">
          <button type="submit" className="btn-primary btn-sm" disabled={busy}>
            Add note
          </button>
        </div>
      </form>

      {notes.length === 0 ? (
        <p className="mt-4 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
          No notes yet.
        </p>
      ) : (
        <ul
          aria-label="Notes"
          className="mt-4 divide-y divide-charcoal-blue-100 dark:divide-white/10"
        >
          {notes.map((note) => (
            <li key={note.id} className="py-3">
              <p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                {note.authorName ||
                  note.authorEmail ||
                  "A former administrator"}{" "}
                · {when(note.createdAt)} UTC
                {note.updatedAt !== note.createdAt &&
                  ` · edited ${when(note.updatedAt)} UTC`}
              </p>
              {editing?.id === note.id ? (
                <div className="mt-2 space-y-2">
                  <label className="sr-only" htmlFor={`note-${note.id}`}>
                    Edit note
                  </label>
                  <textarea
                    id={`note-${note.id}`}
                    rows={3}
                    className="input"
                    value={editing.body}
                    maxLength={4000}
                    onChange={(e) =>
                      setEditing({ id: note.id, body: e.target.value })
                    }
                  />
                  <div className="flex justify-end gap-2">
                    <button
                      type="button"
                      className="btn-ghost btn-sm"
                      onClick={() => setEditing(null)}
                    >
                      Cancel
                    </button>
                    <button
                      type="button"
                      className="btn-primary btn-sm"
                      disabled={busy}
                      onClick={save}
                    >
                      Save
                    </button>
                  </div>
                </div>
              ) : (
                <>
                  <p className="mt-1 whitespace-pre-wrap break-words text-sm text-charcoal-blue-900 dark:text-charcoal-blue-50">
                    {note.body}
                  </p>
                  <div className="mt-1 flex gap-3">
                    <button
                      type="button"
                      className="text-xs text-charcoal-blue-600 hover:underline dark:text-charcoal-blue-300"
                      onClick={() =>
                        setEditing({ id: note.id, body: note.body })
                      }
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      className="text-xs text-destructive hover:underline"
                      disabled={busy}
                      onClick={() => remove(note)}
                    >
                      Delete
                    </button>
                  </div>
                </>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
