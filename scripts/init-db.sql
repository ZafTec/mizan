-- Mizan Database Initialization Script
-- This script runs when the PostgreSQL container is first created

-- Enable UUID extension (required for gen_random_uuid())
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Note: Table creation is handled by migrations:
-- - Auth tables: Drizzle migrations (frontend)
-- - Business tables: EF Core migrations (backend)

-- Note: Data seeding will be added after migrations have run
-- See seed-data.sql for initial data (to be run manually after migrations)
