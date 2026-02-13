/**
 * DATABASE SCHEMA - FRONTEND (DRIZZLE/BETTER-AUTH)
 *
 * ⚠️ CRITICAL: This is the SOURCE OF TRUTH for AUTHENTICATION schema only.
 *
 * SCHEMA OWNERSHIP:
 * =================
 * 
 * ✅ MANAGED BY DRIZZLE (Frontend/BetterAuth):
 *    - users          → BetterAuth user accounts
 *    - accounts       → OAuth provider accounts
 *    - sessions       → User sessions
 *    - verification   → Email verification tokens
 *    - jwks           → JWT signing keys
 *    
 * ⚠️  MANAGED BY EF CORE (Backend) - DO NOT MODIFY VIA DRIZZLE:
 *    - households, householdMembers
 *    - foods, recipes, recipeIngredients, recipeInstructions, recipeNutrition, recipeTags
 *    - foodDiaryEntries
 *    - mealPlans, mealPlanRecipes
 *    - shoppingLists, shoppingListItems
 *    - userGoals
 *    - exercises, workouts, workoutExercises, exerciseSets
 *    - bodyMeasurements
 *    - trainerClientRelationships, chatConversations, chatMessages
 *    - achievements, userAchievements, streaks
 *    - aiChatThreads
 *    - mcpTokens, mcpUsageLogs
 * 
 * ⚠️  MIGRATION WARNING:
 *    Business logic tables are defined here for TYPE SAFETY only.
 *    They are actually managed by EF Core in the backend.
 *    DO NOT run 'drizzle-kit generate' if you've removed these tables
 *    as it will create migrations to DROP them from the database!
 *
 * When modifying AUTH tables, update corresponding backend entities:
 * - Users table → backend/Mizan.Domain/Entities/User.cs (read-only)
 * - Sessions table → backend/Mizan.Domain/Entities/Session.cs (read-only)
 * - Accounts table → backend/Mizan.Domain/Entities/Account.cs (read-only)
 */

import {
  pgTable,
  uuid,
  varchar,
  text,
  boolean,
  timestamp,
  integer,
  decimal,
  date,
  jsonb,
  primaryKey,
  unique,
  index,
} from "drizzle-orm/pg-core";

// ==================== BetterAuth Core Tables ====================

export const users = pgTable("users", {
  id: uuid("id").defaultRandom().primaryKey(),
  email: varchar("email", { length: 255 }).notNull().unique(),
  emailVerified: boolean("email_verified").default(false),
  name: varchar("name", { length: 255 }),
  image: text("image"),
  role: varchar("role", { length: 50 }).default("user"),
  banned: boolean("banned").default(false),
  banReason: text("ban_reason"),
  banExpires: timestamp("ban_expires", { withTimezone: true }),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
});

export const accounts = pgTable(
  "accounts",
  {
    id: uuid("id").defaultRandom().primaryKey(),
    accountId: text("account_id").notNull(),
    providerId: text("provider_id").notNull(),
    userId: uuid("user_id")
      .references(() => users.id, { onDelete: "cascade" })
      .notNull(),
    accessToken: text("access_token"),
    refreshToken: text("refresh_token"),
    idToken: text("id_token"),
    accessTokenExpiresAt: timestamp("access_token_expires_at", { withTimezone: true }),
    refreshTokenExpiresAt: timestamp("refresh_token_expires_at", { withTimezone: true }),
    scope: text("scope"),
    password: text("password"),
    createdAt: timestamp("created_at", { withTimezone: true }).defaultNow().notNull(),
    updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow().notNull(),
  },
  (t) => ({
    userIdIdx: index("accounts_userId_idx").on(t.userId),
  })
);

export const sessions = pgTable("sessions", {
  id: uuid("id").defaultRandom().primaryKey(),
  userId: uuid("user_id")
    .references(() => users.id, { onDelete: "cascade" })
    .notNull(),
  token: varchar("token", { length: 255 }).notNull().unique(),
  expiresAt: timestamp("expires_at", { withTimezone: true }).notNull(),
  ipAddress: text("ip_address"),
  userAgent: text("user_agent"),
  impersonatedBy: uuid("impersonated_by"),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow().notNull(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow().notNull(),
});

export const verification = pgTable("verification", {
  id: uuid("id").defaultRandom().primaryKey(),
  identifier: text("identifier").notNull(),
  value: text("value").notNull(),
  expiresAt: timestamp("expires_at", { withTimezone: true }).notNull(),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
});

export const jwks = pgTable("jwks", {
  id: text("id").primaryKey(),
  publicKey: text("public_key").notNull(),
  privateKey: text("private_key").notNull(),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow().notNull(),
  expiresAt: timestamp("expires_at", { withTimezone: true }),
  alg: varchar("alg", { length: 16 }),
  crv: varchar("crv", { length: 16 }),
});
