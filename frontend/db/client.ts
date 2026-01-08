import { drizzle } from "drizzle-orm/postgres-js";
import postgres from "postgres";
import * as schema from "./schema";

const connectionString =
  process.env.DATABASE_URL || "postgres://mizan:mizan_dev_password@localhost:5432/mizan";

/**
 * Hardened Postgres client:
 * - Disable idle timeout (0) to avoid negative timer underflows
 * - Provide a bounded, non-negative backoff function for reconnects.
 * - Allow a small pool (max 5) which is plenty for Next.js server routes.
 */
const client = postgres(connectionString, {
  max: 5,
  idle_timeout: 0, // no idle culling; prevents setTimeout with negative durations
  connect_timeout: 10,
  backoff: (tries) => {
    const ms = 1000 * Math.min(30, 2 ** tries); // exponential up to 30s
    return Number.isFinite(ms) && ms > 0 ? ms : 1000;
  },
});

export const db = drizzle(client, { schema });

export type Database = typeof db;
