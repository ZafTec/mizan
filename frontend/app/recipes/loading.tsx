import DirectionalTransition from "@/components/motion/DirectionalTransition";
import { CardGridSkeleton } from "@/components/ui/skeletons";

export default function Loading() {
  return (
    <DirectionalTransition fallback>
      <CardGridSkeleton />
    </DirectionalTransition>
  );
}
