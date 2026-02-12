"use client";

import { createAuthClient } from "better-auth/react";
import { organizationClient, adminClient } from "better-auth/client/plugins";
import { ac, adminRole, trainerRole, userRole } from "@/lib/permissions";

const betterAuthUrl = process.env.NEXT_PUBLIC_APP_URL || process.env.NEXT_PUBLIC_BETTER_AUTH_URL;

if (!betterAuthUrl) {
  throw new Error("NEXT_PUBLIC_APP_URL or NEXT_PUBLIC_BETTER_AUTH_URL must be defined");
}

export const authClient = createAuthClient({
  baseURL: betterAuthUrl,
  plugins: [
    organizationClient(),
    adminClient({
      ac,
      roles: {
        user: userRole,
        trainer: trainerRole,
        admin: adminRole,
      },
    }),
  ],
});

export const {
  signIn,
  signUp,
  signOut,
  useSession,
  getSession,
  organization,
} = authClient;
