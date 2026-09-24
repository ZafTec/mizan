"use client";

import {
  initializePaddle,
  type Paddle,
  type PaddleEventData,
  type Environments,
  type CheckoutOpenOptions,
} from "@paddle/paddle-js";
import { clientApi } from "@/lib/api.client";

const TOKEN = process.env.NEXT_PUBLIC_PADDLE_CLIENT_TOKEN;
const ENVIRONMENT = (process.env.NEXT_PUBLIC_PADDLE_ENV as Environments) ?? "sandbox";

// Single cached Paddle instance. The eventCallback passed on first init wins,
// so callers that need checkout events (the billing page) should init first.
let paddlePromise: Promise<Paddle | undefined> | null = null;

export function isPaddleConfigured(): boolean {
  return Boolean(TOKEN);
}

export function getPaddle(eventCallback?: (event: PaddleEventData) => void): Promise<Paddle | undefined> {
  if (!TOKEN) {
    return Promise.resolve(undefined);
  }
  if (!paddlePromise) {
    paddlePromise = initializePaddle({ token: TOKEN, environment: ENVIRONMENT, eventCallback });
  }
  return paddlePromise;
}

/**
 * Opens Paddle's overlay checkout for one plan. The price and any automatic
 * deal come from the plan list the API serves (GET /api/Subscriptions/plans),
 * never from build-time configuration, so an admin's change is live at once.
 */
export async function openCheckout(params: {
  priceId: string;
  discountId?: string | null;
  userId: string;
  email?: string;
  eventCallback?: (event: PaddleEventData) => void;
}): Promise<boolean> {
  const paddle = await getPaddle(params.eventCallback);
  if (!paddle) {
    return false;
  }

  const options: CheckoutOpenOptions = {
    items: [{ priceId: params.priceId, quantity: 1 }],
    customData: { user_id: params.userId },
    ...(params.email ? { customer: { email: params.email } } : {}),
    ...(params.discountId ? { discountId: params.discountId } : {}),
  };

  paddle.Checkout.open(options);
  return true;
}

export interface BillingPortalSession {
  overviewUrl: string;
  cancelSubscriptionUrl: string | null;
  updatePaymentMethodUrl: string | null;
}

/**
 * Updating a card happens on a page Paddle hosts and secures - never on ours.
 * This mints a fresh link to it; the link is single-use, so it is fetched
 * right before opening, never stored.
 */
export async function getBillingPortal(): Promise<BillingPortalSession> {
  return clientApi<BillingPortalSession>("/api/Subscriptions/portal", { method: "POST" });
}
