import { defineConfig } from "drizzle-kit";

export default defineConfig({
  schema: "./db/schema.ts",
  out: "./db/migrations",
  dialect: "postgresql",
  dbCredentials: {
    url: process.env.DATABASE_URL || "postgres://mizan:mizan_dev_password@localhost:5432/mizan",
  },
  tablesFilter: ["!__*"], // Exclude tables starting with __ (e.g., __EFMigrationsHistory)
});
