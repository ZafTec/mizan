import DirectionalTransition from "@/components/motion/DirectionalTransition";
import MiniAppGate from "@/components/tg/MiniAppGate";
import MiniAppLog from "@/components/tg/MiniAppLog";

/** "Log a meal" from the Mini App home: the web's meal form, inside /tg. */
export default function MiniAppLogPage() {
  return (
    <DirectionalTransition>
      <MiniAppGate>
        <MiniAppLog />
      </MiniAppGate>
    </DirectionalTransition>
  );
}
