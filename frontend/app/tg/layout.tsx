import type { Metadata } from "next";
import MiniAppShell from "@/components/tg/MiniAppShell";
import "./miniapp.css";

export const metadata: Metadata = {
  title: "Mizan",
  robots: { index: false, follow: false },
};

/**
 * The Telegram Mini App without the shell: /tg (home), /tg/log, /tg/plan,
 * /tg/shopping-list, and not-found.tsx for any other /tg path.
 * The pages sign themselves in from Telegram's initData (docs/TELEGRAM.md#mini-app).
 */
export default function MiniAppLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return <MiniAppShell>{children}</MiniAppShell>;
}
