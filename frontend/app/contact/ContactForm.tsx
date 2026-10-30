"use client";

import { useState } from "react";
import { useSession } from "@/components/SessionProvider";
import { submitContact } from "@/lib/api/admin-ops";
import {
  CONTACT_LIMITS,
  contactSchema,
  fieldErrors,
  type ContactForm as ContactValues,
} from "@/lib/admin-ops/schema";
import { getErrorMessage } from "@/lib/toast";

type Field = keyof ContactValues;

const FIELDS: {
  name: Field;
  label: string;
  type?: string;
  autoComplete?: string;
  multiline?: boolean;
}[] = [
  { name: "name", label: "Name", autoComplete: "name" },
  { name: "email", label: "Email", type: "email", autoComplete: "email" },
  { name: "subject", label: "Subject" },
  { name: "message", label: "Message", multiline: true },
];

/**
 * Name, email, subject and message, checked here and again by the API. The
 * "website" field is hidden from people and from screen readers; a bot that
 * fills it is accepted and dropped by the API, and a person's empty one is
 * never sent.
 */
export default function ContactForm() {
  const { data } = useSession();
  const user = data?.user;
  const [values, setValues] = useState<ContactValues>({
    name: user?.name ?? "",
    email: user?.email ?? "",
    subject: "",
    message: "",
  });
  const [website, setWebsite] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [state, setState] = useState<"idle" | "sending" | "sent">("idle");

  const submit = async () => {
    const parsed = contactSchema.safeParse(values);
    if (!parsed.success) {
      setErrors(fieldErrors(parsed.error));
      return;
    }
    setErrors({});
    setState("sending");
    try {
      await submitContact({
        ...parsed.data,
        ...(website ? { website } : {}),
      });
      setState("sent");
    } catch (error) {
      setErrors({
        form: getErrorMessage(
          error,
          "Your message did not go through. Try again in a moment.",
        ),
      });
      setState("idle");
    }
  };

  if (state === "sent")
    return (
      <div role="status" className="card space-y-3 p-6">
        <h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
          Thanks, your message is on its way
        </h2>
        <p className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
          We reply to {values.email}, usually within two working days.
        </p>
        <button
          type="button"
          className="btn-ghost btn-sm"
          onClick={() => {
            setValues((v) => ({ ...v, subject: "", message: "" }));
            setState("idle");
          }}
        >
          Write another
        </button>
      </div>
    );

  return (
    <form
      className="card space-y-4 p-5 sm:p-6"
      noValidate
      onSubmit={(e) => {
        e.preventDefault();
        void submit();
      }}
    >
      {FIELDS.map((field) => {
        const id = `contact-${field.name}`;
        const error = errors[field.name];
        const common = {
          id,
          name: field.name,
          value: values[field.name],
          maxLength: CONTACT_LIMITS[field.name],
          className: `input ${error ? "input-error" : ""}`,
          "aria-invalid": Boolean(error),
          "aria-describedby": error ? `${id}-error` : undefined,
          onChange: (
            e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>,
          ) => setValues((v) => ({ ...v, [field.name]: e.target.value })),
        };
        return (
          <div key={field.name}>
            <label className="label" htmlFor={id}>
              {field.label}
            </label>
            {field.multiline ? (
              <textarea rows={7} {...common} />
            ) : (
              <input
                type={field.type ?? "text"}
                autoComplete={field.autoComplete}
                {...common}
              />
            )}
            {error && (
              <p
                id={`${id}-error`}
                className="mt-1 text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
              >
                {error}
              </p>
            )}
          </div>
        );
      })}

      {/* Hidden from people: off screen, out of the tab order and the accessibility tree. */}
      <div
        aria-hidden="true"
        className="absolute -left-[10000px] h-px w-px overflow-hidden"
      >
        <label htmlFor="contact-website">Website</label>
        <input
          id="contact-website"
          name="website"
          type="text"
          tabIndex={-1}
          autoComplete="off"
          value={website}
          onChange={(e) => setWebsite(e.target.value)}
        />
      </div>

      {errors.form && (
        <p
          role="alert"
          className="text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
        >
          {errors.form}
        </p>
      )}

      <div className="flex justify-end">
        <button
          type="submit"
          className="btn-primary"
          disabled={state === "sending"}
        >
          {state === "sending" ? "Sending…" : "Send message"}
        </button>
      </div>
    </form>
  );
}
