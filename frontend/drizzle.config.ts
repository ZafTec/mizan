import { defineConfig } from "drizzle-kit";

/**
 * Drizzle configuration for Better Auth tables only.
 *
 * ⚠️ IMPORTANT: This database is SHARED with the backend (.NET/EF Core).
 * - Drizzle manages ONLY Better Auth tables: users, accounts, sessions, verification, jwks
 * - EF Core manages all business logic tables: recipes, foods, workouts, etc.
 *
 * The tablesFilter ensures Drizzle won't touch EF Core tables or its migration history.
 * Existing migrations may reference backend tables (created before this split), but
 * drizzle-kit migrate will only apply changes to the tables listed below.
 */
export default defineConfig({
  schema: "./db/schema.ts",
  out: "./db/migrations",
  dialect: "postgresql",
  dbCredentials: {
    url: process.env.DATABASE_URL || "postgres://mizan:mizan_dev_password@localhost:5432/mizan",
  },
  // Only manage Better Auth tables - exclude EF Core tables and migration history
  tablesFilter: [
    "users",
    "accounts",
    "sessions",
    "verification",
    "jwks",
    "!__*", // Exclude EF Core __EFMigrationsHistory
  ],
});
