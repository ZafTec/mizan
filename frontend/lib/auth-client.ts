"use client";

import { createAuthClient } from "better-auth/react";
import { adminClient, magicLinkClient } from "better-auth/client/plugins";
import { ac, adminRole, trainerRole, userRole } from "@/lib/permissions";
import { sentinelClient } from "@better-auth/infra/client";

const betterAuthUrl = process.env.NEXT_PUBLIC_APP_URL || process.env.NEXT_PUBLIC_BETTER_AUTH_URL;

if (!betterAuthUrl) {
  throw new Error("NEXT_PUBLIC_APP_URL or NEXT_PUBLIC_BETTER_AUTH_URL must be defined");
}

export const authClient = createAuthClient({
  baseURL: betterAuthUrl,
  fetchOptions: {
    onError: async (context) => {
      if (context.response.status === 429) {
        const retryAfter = context.response.headers.get("X-Retry-After");
        console.warn(
          `[auth] Rate limited. Retry after ${retryAfter ?? "unknown"} seconds.`,
        );
      }
    },
  },
  plugins: [
    magicLinkClient(),
    adminClient({
      ac,
      roles: {
        user: userRole,
        trainer: trainerRole,
        admin: adminRole,
      },
    }),
    sentinelClient(),
  ],
});

export const {
  signIn,
  signUp,
  signOut,
  useSession,
  getSession,
} = authClient;
