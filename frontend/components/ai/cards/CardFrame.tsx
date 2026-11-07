import Link from "next/link";

/**
 * The frame every chat card shares: a ruled box under the reply with a
 * caption, the card's content, and a link to the page it came from.
 */
export default function CardFrame({
  caption,
  label,
  href,
  linkText,
  children,
}: {
  caption: string;
  label: string;
  href?: string;
  linkText?: string;
  children: React.ReactNode;
}) {
  return (
    <section
      aria-label={label}
      className="w-full space-y-3 rounded-md border border-border bg-background px-3.5 py-3 text-sm"
    >
      <p className="eyebrow">{caption}</p>
      {children}
      {href && linkText && (
        <p>
          <Link href={href} className="text-link">
            {linkText}
          </Link>
        </p>
      )}
    </section>
  );
}
