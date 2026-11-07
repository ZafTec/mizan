"use client";

import {
  initializePaddle,
  type Paddle,
  type PaddleEventData,
  type Environments,
  type CheckoutOpenOptions,
} from "@paddle/paddle-js";
import { clientApi } from "@/lib/api.client";
import type { components } from "@/types/api.generated";

type CheckoutSettings = components["schemas"]["CheckoutSettingsDto"];

// The client-side token and environment are runtime settings the API serves
// (GET /api/Subscriptions/checkout-settings), read when checkout first opens.
// Single cached Paddle instance. The eventCallback passed on first init wins,
// so callers that need checkout events (the billing page) should init first.
let paddlePromise: Promise<Paddle | undefined> | null = null;

async function loadPaddle(
  eventCallback?: (event: PaddleEventData) => void,
): Promise<Paddle | undefined> {
  const settings = await clientApi<CheckoutSettings>(
    "/api/Subscriptions/checkout-settings",
  );
  if (!settings.clientToken) {
    return undefined;
  }
  return initializePaddle({
    token: settings.clientToken,
    environment: settings.environment as Environments,
    eventCallback,
  });
}

export function getPaddle(
  eventCallback?: (event: PaddleEventData) => void,
): Promise<Paddle | undefined> {
  if (!paddlePromise) {
    paddlePromise = loadPaddle(eventCallback).then(
      (paddle) => {
        // Not configured yet: ask again next time rather than remembering "off".
        if (!paddle) paddlePromise = null;
        return paddle;
      },
      (error: unknown) => {
        paddlePromise = null;
        throw error;
      },
    );
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
    // "product" separates Mizan from other ZafTech apps on the same Paddle account.
    customData: { product: "mizan", user_id: params.userId },
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
  return clientApi<BillingPortalSession>("/api/Subscriptions/portal", {
    method: "POST",
  });
}
