// Shape of GET /api/Subscriptions/me after apiClient camelCase conversion.
export interface MySubscription {
  plan: string; // free | pro | lifetime
  status: string; // none | trialing | active | past_due | paused | canceled
  isPro: boolean;
  isLifetime: boolean;
  currentPeriodEnd: string | null;
  trialEndsAt: string | null;
  canceledAt: string | null;
}
