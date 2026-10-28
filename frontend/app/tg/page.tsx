import MiniAppGate from "@/components/tg/MiniAppGate";
import MiniAppHome from "@/components/tg/MiniAppHome";

/** The Mini App's first page: the bot's menu button opens /tg. */
export default function MiniAppHomePage() {
  return (
    <MiniAppGate>
      <MiniAppHome />
    </MiniAppGate>
  );
}
