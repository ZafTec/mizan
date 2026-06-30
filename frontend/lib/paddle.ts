"use client";

import {
  initializePaddle,
  type Paddle,
  type PaddleEventData,
  type Environments,
  type CheckoutOpenOptions,
} from "@paddle/paddle-js";

const TOKEN = process.env.NEXT_PUBLIC_PADDLE_CLIENT_TOKEN;
const ENVIRONMENT = (process.env.NEXT_PUBLIC_PADDLE_ENV as Environments) ?? "sandbox";

export const PADDLE_PRICES = {
  proMonthly: process.env.NEXT_PUBLIC_PADDLE_PRICE_PRO_MONTHLY ?? "",
  proYearly: process.env.NEXT_PUBLIC_PADDLE_PRICE_PRO_YEARLY ?? "",
  lifetime: process.env.NEXT_PUBLIC_PADDLE_PRICE_LIFETIME ?? "",
} as const;

export type PaddlePlan = keyof typeof PADDLE_PRICES;

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

export async function openCheckout(params: {
  priceId: string;
  userId: string;
  email?: string;
  eventCallback?: (event: PaddleEventData) => void;
}): Promise<boolean> {
  const paddle = await getPaddle(params.eventCallback);
  if (!paddle) {
    return false;
  }

  const base = {
    items: [{ priceId: params.priceId, quantity: 1 }],
    customData: { user_id: params.userId },
  };
  const options: CheckoutOpenOptions = params.email
    ? { ...base, customer: { email: params.email } }
    : base;

  paddle.Checkout.open(options);
  return true;
}
