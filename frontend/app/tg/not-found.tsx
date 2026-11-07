import Link from "next/link";

/** A /tg link that leads nowhere: one sentence and the way back, inside the Mini App. */
export default function MiniAppNotFound() {
  return (
    <section
      className="log-page space-y-4"
      aria-labelledby="tg-not-found"
      data-testid="tg-not-found"
    >
      <h1 id="tg-not-found" className="text-base font-normal">
        This page is not in the Mini App.
      </h1>
      <Link href="/tg" className="btn-primary min-h-11">
        Back to today
      </Link>
    </section>
  );
}
