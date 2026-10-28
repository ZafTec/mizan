import DirectionalTransition from "@/components/motion/DirectionalTransition";
import MiniAppGate from "@/components/tg/MiniAppGate";
import MiniAppPlan from "@/components/tg/MiniAppPlan";

type Search = Promise<Record<string, string | string[] | undefined>>;

const one = (value: string | string[] | undefined) =>
  Array.isArray(value) ? value[0] : value || undefined;

/** The /plan card's Open button: /tg/plan?planId=...&day=yyyy-MM-dd. */
export default async function MiniAppPlanPage({
  searchParams,
}: {
  searchParams: Search;
}) {
  const params = await searchParams;
  return (
    <DirectionalTransition>
      <MiniAppGate>
        <MiniAppPlan planId={one(params.planId)} day={one(params.day)} />
      </MiniAppGate>
    </DirectionalTransition>
  );
}
