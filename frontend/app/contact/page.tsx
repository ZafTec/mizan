import ContactForm from "./ContactForm";

export const metadata = {
  title: "Contact | Mizan",
  description: "Write to the people who run Mizan",
};

/**
 * The public contact form (docs/ADMIN.md#contact). Signed out is fine; a
 * signed-in person's name and address are filled in, and the message is
 * linked to their account by the API from the session.
 */
export default function ContactPage() {
  return (
    <div className="mx-auto max-w-xl space-y-6">
      <header className="space-y-2">
        <p className="eyebrow">Contact</p>
        <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
          Write to us
        </h1>
        <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
          A question, a problem, or an idea. We reply by email.
        </p>
      </header>
      <ContactForm />
    </div>
  );
}
