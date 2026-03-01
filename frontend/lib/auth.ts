import "server-only";
import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { admin, haveIBeenPwned, jwt, lastLoginMethod, magicLink } from "better-auth/plugins";
import { eq } from "drizzle-orm";
import { db } from "@/db/client";
import {
  sendEmail,
  getVerificationEmailTemplate,
  getPasswordResetEmailTemplate,
  getMagicLinkEmailTemplate,
  getSignInNotificationEmailTemplate,
} from "@/lib/email";
import { ac, adminRole, trainerRole, userRole } from "@/lib/permissions";
import { logger } from "@/lib/logger";
import { getBetterAuthSecondaryStorage } from "@/lib/redis";

import * as schema from "@/db/schema";

import { dash } from "@better-auth/infra";

const authLogger = logger.createModuleLogger("auth-server");

authLogger.debug("BetterAuth environment variables", {
  BETTER_AUTH_URL: process.env.BETTER_AUTH_URL,
  BETTER_AUTH_ISSUER: process.env.BETTER_AUTH_ISSUER,
  BETTER_AUTH_AUDIENCE: process.env.BETTER_AUTH_AUDIENCE,
  API_URL: process.env.API_URL,
  NODE_ENV: process.env.NODE_ENV,
  REDIS_URL: process.env.REDIS_URL ?? "<not set>",
});

const hasRedis = !!process.env.REDIS_URL;

export const auth = betterAuth({
  baseURL: process.env.BETTER_AUTH_URL || "http://localhost:3000",
  database: drizzleAdapter(db, {
    provider: "pg",
    schema: {
      ...schema,
      user: schema.users,
      session: schema.sessions,
      account: schema.accounts,
      verification: schema.verification,
    },
  }),
  secondaryStorage: hasRedis ? getBetterAuthSecondaryStorage() : undefined,
  emailAndPassword: {
    enabled: true,
    requireEmailVerification: true,
    sendResetPassword: async ({ user, url }) => {
      if (process.env.NODE_ENV !== "production") {
        authLogger.debug("Password reset requested", { email: user.email, resetUrl: url });
      }

      try {
        const emailTemplate = getPasswordResetEmailTemplate(url, user.name);
        await sendEmail({
          to: user.email,
          subject: emailTemplate.subject,
          html: emailTemplate.html,
          text: emailTemplate.text,
        });
        authLogger.info("Password reset email sent", { email: user.email });
      } catch (error) {
        authLogger.error("Failed to send password reset email", {
          email: user.email,
          error: error instanceof Error ? error.message : String(error),
        });
        throw error;
      }
    },
  },
  emailVerification: {
    sendVerificationEmail: async ({ user, url }) => {
      authLogger.info(`ENV - BETTER_AUTH_URL: ${process.env.BETTER_AUTH_URL} ENV - NODE_ENV: ${process.env.NODE_ENV}`);

      if (process.env.NODE_ENV !== "production") {
        authLogger.debug("Email verification requested", { email: user.email });
        authLogger.debug(`Verification URL: ${url}`);
      }

      try {
        const emailTemplate = getVerificationEmailTemplate(url, user.name);
        authLogger.debug("Sending verification email", { email: user.email });
        await sendEmail({
          to: user.email,
          subject: emailTemplate.subject,
          html: emailTemplate.html,
          text: emailTemplate.text,
        });
        authLogger.info("Verification email sent", { email: user.email });
      } catch (error) {
        authLogger.error("Failed to send verification email", {
          email: user.email,
          error: error instanceof Error ? error.message : String(error),
        });
        throw error;
      }
    },
    sendOnSignUp: true,
    autoSignInAfterVerification: true,
  },
  socialProviders: {
    google: {
      clientId: process.env.GOOGLE_CLIENT_ID!,
      clientSecret: process.env.GOOGLE_CLIENT_SECRET!,
    },
    github: {
      clientId: process.env.GITHUB_CLIENT_ID!,
      clientSecret: process.env.GITHUB_CLIENT_SECRET!,
    },
  },
  plugins: [
    jwt({
      jwt: {
        issuer: process.env.BETTER_AUTH_ISSUER,
        audience: process.env.BETTER_AUTH_AUDIENCE,
      },
    }),
    admin({
      defaultRole: "user",
      adminRoles: ["admin"],
      impersonationSessionDuration: 60 * 60 * 24,
      allowImpersonatingAdmins: false,
      ac,
      roles: {
        user: userRole,
        trainer: trainerRole,
        admin: adminRole,
      },
    }),
    magicLink({
      expiresIn: 60 * 15, // 15 minutes
      disableSignUp: true, // only existing users can use magic link on forgot-password
      sendMagicLink: async ({ email, url }) => {
        authLogger.debug("Magic link requested", { email, url });
        try {
          const emailTemplate = getMagicLinkEmailTemplate(url);
          await sendEmail({
            to: email,
            subject: emailTemplate.subject,
            html: emailTemplate.html,
            text: emailTemplate.text,
          });
          authLogger.info("Magic link email sent", { email });
        } catch (error) {
          authLogger.error("Failed to send magic link email", {
            email,
            error: error instanceof Error ? error.message : String(error),
          });
          throw error;
        }
      },
    }),
    dash(),
    lastLoginMethod(),
    haveIBeenPwned(),
  ],
  session: {
    expiresIn: 60 * 60 * 24 * 7, // 7 days
    updateAge: 60 * 60 * 24, // 1 day
    storeSessionInDatabase: true, // persist to DB even when Redis is the primary store
    cookieCache: {
      enabled: true,
      maxAge: 5 * 60, // 5 minutes
    },
  },
  rateLimit: {
    window: 60,        // global: 100 req / 60s per IP
    max: 100,
    storage: hasRedis ? "secondary-storage" : "database",
    customRules: {
      // Credential endpoints — tight window to limit brute-force
      "/sign-in/email":              { window: 10,  max: 5  },
      "/sign-up/email":              { window: 60,  max: 5  },
      "/forget-password":            { window: 60,  max: 3  },
      "/reset-password":             { window: 60,  max: 5  },
      // Magic link — each send costs an email; keep it strict
      "/magic-link/send-magic-link": { window: 300, max: 3  },
      // Verification resends — prevent inbox flooding
      "/send-verification-email":    { window: 60,  max: 3  },
      // High-frequency legit traffic — exempt from rate limiting
      "/get-session": false,
      "/sign-out":    false,
    },
  },
  databaseHooks: {
    session: {
      create: {
        after: async (session) => {
          try {
            const result = await db
              .select({ email: schema.users.email })
              .from(schema.users)
              .where(eq(schema.users.id, session.userId))
              .limit(1);

            const user = result[0];
            if (!user) return;

            const emailTemplate = getSignInNotificationEmailTemplate(
              session.ipAddress,
              session.userAgent,
            );
            await sendEmail({
              to: user.email,
              subject: emailTemplate.subject,
              html: emailTemplate.html,
              text: emailTemplate.text,
            });
            authLogger.info("Sign-in notification sent", { email: user.email });
          } catch (error) {
            authLogger.error("Failed to send sign-in notification", {
              userId: session.userId,
              error: error instanceof Error ? error.message : String(error),
            });
          }
        },
      },
    },
  },
  advanced: {
    database: {
      generateId: () => Bun.randomUUIDv7(),
    },
    cookies: {
      session_token: {
        attributes: {
          httpOnly: true,
          sameSite: "lax",
          secure: process.env.NODE_ENV === "production",
          path: "/",
        },
      },
    },
  },
  trustedOrigins: [
    process.env.BETTER_AUTH_URL!,
    process.env.API_URL!,
  ]
});

export type Session = typeof auth.$Infer.Session;
export type User = typeof auth.$Infer.Session.user;
