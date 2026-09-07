import type { GamificationFeedback } from "@/types/gamification";

export const LOG_FEEDBACK_EVENT = "mizan:log-feedback";

/** Keep unlock feedback in the shell so closing a log sheet cannot swallow it. */
export function publishLogFeedback(feedback: GamificationFeedback | undefined) {
  if (!feedback?.streak?.extended && !feedback?.unlockedAchievements?.length) return;
  window.dispatchEvent(new CustomEvent(LOG_FEEDBACK_EVENT, { detail: feedback }));
}
