import "server-only";
import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { jwt, organization, admin } from "better-auth/plugins";
import { db } from "@/db/client";
import { sendEmail, getVerificationEmailTemplate, getPasswordResetEmailTemplate } from "@/lib/email";
import { ac, adminRole, trainerRole, userRole } from "@/lib/permissions";

import * as schema from "@/db/schema";

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
      // Log URL in development for easy testing
      if (process.env.NODE_ENV !== "production") {
        console.log(`\n🔐 Password Reset for ${user.email}`);
        console.log(`🔗 Reset URL: ${url}`);
        console.log(`\nDEV MODE: Click the link above to reset your password\n`);
      }

      // Send email
      const emailTemplate = getPasswordResetEmailTemplate(url, user.name);
      await sendEmail({
        to: user.email,
        subject: emailTemplate.subject,
        html: emailTemplate.html,
        text: emailTemplate.text,
      });
    },
  },
  emailVerification: {
    sendVerificationEmail: async ({ user, url, token }) => {
      // Log URL in development for easy testing
      if (process.env.NODE_ENV !== "production") {
        console.log(`\n📧 Email Verification for ${user.email}`);
        console.log(`🔗 Verification URL: ${url}`);
        console.log(`🎫 Token: ${token}`);
        console.log(`\nDEV MODE: Click the link above to verify your email\n`);
      }

      // Send email
      const emailTemplate = getVerificationEmailTemplate(url, user.name);
      await sendEmail({
        to: user.email,
        subject: emailTemplate.subject,
        html: emailTemplate.html,
        text: emailTemplate.text,
      });
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
        issuer: process.env.BETTER_AUTH_URL || "http://localhost:3000",
        audience: "mizan-api",
        expirationTime: "15m",
      },
      jwks: {
        keyPairConfig: {
          alg: "ES256", // Using ES256 (ECDSA P-256, natively supported by .NET)
        },
      },
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
      generateId: () => crypto.randomUUID(),
    },
    cookies: {
      sessionToken: {
        name: "better-auth.session_token",
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
    process.env.BETTER_AUTH_URL || "http://localhost:3000",
    process.env.API_URL || "http://localhost:5000",
  ],
});

export type Session = typeof auth.$Infer.Session;
export type User = typeof auth.$Infer.Session.user;
