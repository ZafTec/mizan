import DirectionalTransition from "@/components/motion/DirectionalTransition";
import { DetailSkeleton } from "@/components/ui/skeletons";

export default function Loading() {
  return (
    <DirectionalTransition>
      <DetailSkeleton />
    </DirectionalTransition>
  );
}
