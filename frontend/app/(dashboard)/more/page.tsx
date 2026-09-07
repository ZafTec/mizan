import type { Metadata } from "next";
import { getUserOptionalServer } from "@/helper/session";
import { visibleGroups } from "@/components/Layout/nav";
import MoreDirectory from "./MoreDirectory";

export const metadata: Metadata = {
  title: "More · Mizan",
};

/**
 * Tier 3 - see docs/ARCHITECTURE.md#navigation-and-logging.
 *
 * Everything the product does that is not logging. One tap from the spine,
 * zero permanent pixels until asked for. The list itself is filtered client
 * side; which items exist at all still depends on the role resolved here.
 */
export default async function MorePage() {
  const user = await getUserOptionalServer();
  const groups = visibleGroups(user?.role === "admin");

  return (
    <div className="log-page">
      <header className="log-page-header block!">
        <h1>More</h1>
        <p className="log-muted mt-2">Everything beyond your daily log.</p>
      </header>

      <MoreDirectory groups={groups} />
    </div>
  );
}
