import "server-only";
import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { jwt, organization, admin } from "better-auth/plugins";
import { db } from "@/db/client";
import { sendEmail, getVerificationEmailTemplate, getPasswordResetEmailTemplate } from "@/lib/email";
import { ac, adminRole, trainerRole, userRole } from "@/lib/permissions";
import { logger } from "@/lib/logger";

import * as schema from "@/db/schema";

const authLogger = logger.createModuleLogger("auth-server");

export const auth = betterAuth({
  database: drizzleAdapter(db, {
    provider: "pg",
    schema: {
      ...schema,
      user: schema.users,
      session: schema.sessions,
      account: schema.accounts,
      jwks: schema.jwks,
      verification: schema.verification,
    },
  }),
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
    sendVerificationEmail: async ({ user, url, token }) => {
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
      jwks: {
        keyPairConfig: {
          alg: "EdDSA",
          crv: "Ed25519"
        }
      },
      jwt: {
        issuer: process.env.BETTER_AUTH_URL!,
        expirationTime: "15m",
      }
    }),
    organization({
      // Maps to household concept
      allowUserToCreateOrganization: true,
      organizationLimit: 5,
    }),
    admin({
      defaultRole: "user",
      adminRoles: ["admin"], // Only system admins, NOT trainers
      impersonationSessionDuration: 60 * 60 * 24, // 24 hours
      allowImpersonatingAdmins: false,
      ac, // Access control configuration
      roles: {
        user: userRole,
        trainer: trainerRole,
        admin: adminRole,
      },
    }),
  ],
  session: {
    expiresIn: 60 * 60 * 24 * 7, // 7 days
    updateAge: 60 * 60 * 24, // 1 day
    cookieCache: {
      enabled: true,
      maxAge: 5 * 60, // 5 minutes
    },
  },
  rateLimit: {
    window: 60,
    max: 100,
  },
  advanced: {
    database: {
      generateId: () => Bun.randomUUIDv7(),
    },
    cookies: {
      sessionToken: {
        name: "mizan.session_token",
        options: {
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
  ],
});

export type Session = typeof auth.$Infer.Session;
export type User = typeof auth.$Infer.Session.user;
