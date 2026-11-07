import MarketingOptOut from "./MarketingOptOut";

export const metadata = {
  title: "Turn off news and offers | Mizan",
  description: "Stop product news and offers from Mizan",
  robots: { index: false },
};

/**
 * /notifications/opt-out?token=…: the link in a marketing email. It asks for
 * one press rather than acting on load, so a mail scanner that opens links
 * does not turn anything off.
 */
export default async function MarketingOptOutPage({
  searchParams,
}: {
  searchParams: Promise<{ token?: string | string[] }>;
}) {
  const { token } = await searchParams;
  return (
    <div className="flex min-h-[70vh] items-center justify-center px-4">
      <div className="w-full max-w-md space-y-4">
        <h1 className="text-3xl font-semibold tracking-tight">
          News and offers
        </h1>
        <MarketingOptOut token={typeof token === "string" ? token : null} />
      </div>
    </div>
  );
}
